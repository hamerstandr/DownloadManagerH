using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت connection pool برای کلاینت‌های HTTP
    /// </summary>
    public class ConnectionPoolManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, HttpClientPool> _pools;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed = false;

        // تنظیمات پیش‌فرض
        public int MaxConnectionsPerHost { get; set; } = 10;
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
        public int MaxPoolSize { get; set; } = 50;

        public ConnectionPoolManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pools = new ConcurrentDictionary<string, HttpClientPool>();
            _semaphore = new SemaphoreSlim(1, 1);

            // تایمر پاک‌سازی هر 30 ثانیه
            _cleanupTimer = new Timer(CleanupExpiredConnections, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            _logger.LogInfo("ConnectionPoolManager مقداردهی اولیه شد");
        }

        /// <summary>
        /// دریافت HttpClient از pool
        /// </summary>
        public async Task<PooledHttpClient> GetHttpClientAsync(string host = "default")
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectionPoolManager));

            var pool = _pools.GetOrAdd(host, _ => new HttpClientPool(host, MaxConnectionsPerHost, _logger));
            return await pool.GetClientAsync();
        }

        /// <summary>
        /// بازگرداندن HttpClient به pool
        /// </summary>
        public void ReturnHttpClient(PooledHttpClient client)
        {
            if (client == null || _disposed) return;

            if (_pools.TryGetValue(client.PoolKey, out var pool))
            {
                pool.ReturnClient(client);
            }
            else
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// پاک‌سازی اتصالات منقضی شده
        /// </summary>
        private async void CleanupExpiredConnections(object? state)
        {
            if (_disposed) return;

            try
            {
                await _semaphore.WaitAsync();

                var expiredPools = new List<string>();
                
                foreach (var kvp in _pools)
                {
                    var pool = kvp.Value;
                    pool.CleanupExpiredClients(IdleTimeout);

                    // اگر pool خالی است و مدت زیادی استفاده نشده، آن را حذف کن
                    if (pool.IsEmpty && pool.LastUsed.Add(TimeSpan.FromMinutes(10)) < DateTime.Now)
                    {
                        expiredPools.Add(kvp.Key);
                    }
                }

                // حذف pool های منقضی شده
                foreach (var poolKey in expiredPools)
                {
                    if (_pools.TryRemove(poolKey, out var pool))
                    {
                        pool.Dispose();
                        _logger.LogDebug($"Pool منقضی شده حذف شد: {poolKey}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پاک‌سازی connection pool", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// دریافت آمار connection pool
        /// </summary>
        public ConnectionPoolStatistics GetStatistics()
        {
            var stats = new ConnectionPoolStatistics
            {
                TotalPools = _pools.Count,
                TotalConnections = 0,
                ActiveConnections = 0,
                IdleConnections = 0
            };

            foreach (var pool in _pools.Values)
            {
                var poolStats = pool.GetStatistics();
                stats.TotalConnections += poolStats.TotalClients;
                stats.ActiveConnections += poolStats.ActiveClients;
                stats.IdleConnections += poolStats.IdleClients;
            }

            return stats;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                _cleanupTimer?.Dispose();
                
                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }
                _pools.Clear();
                
                _semaphore?.Dispose();
                
                _logger.LogInfo("ConnectionPoolManager disposed");
            }
        }
    }

    /// <summary>
    /// Pool برای مدیریت HttpClient های یک host خاص
    /// </summary>
    internal class HttpClientPool : IDisposable
    {
        private readonly string _host;
        private readonly int _maxSize;
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<PooledHttpClient> _availableClients;
        private readonly ConcurrentDictionary<int, PooledHttpClient> _activeClients;
        private readonly SemaphoreSlim _semaphore;
        private int _totalCreated = 0;
        private bool _disposed = false;

        public DateTime LastUsed { get; private set; } = DateTime.Now;
        public bool IsEmpty => _availableClients.IsEmpty && _activeClients.IsEmpty;

        public HttpClientPool(string host, int maxSize, ILogger logger)
        {
            _host = host;
            _maxSize = maxSize;
            _logger = logger;
            _availableClients = new ConcurrentQueue<PooledHttpClient>();
            _activeClients = new ConcurrentDictionary<int, PooledHttpClient>();
            _semaphore = new SemaphoreSlim(maxSize, maxSize);
        }

        public async Task<PooledHttpClient> GetClientAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClientPool));

            await _semaphore.WaitAsync();
            LastUsed = DateTime.Now;

            try
            {
                // سعی در دریافت client موجود از pool
                if (_availableClients.TryDequeue(out var existingClient))
                {
                    if (!existingClient.IsExpired)
                    {
                        existingClient.MarkAsActive();
                        _activeClients.TryAdd(existingClient.Id, existingClient);
                        return existingClient;
                    }
                    else
                    {
                        existingClient.Dispose();
                    }
                }

                // ایجاد client جدید
                var newClient = CreateNewClient();
                _activeClients.TryAdd(newClient.Id, newClient);
                return newClient;
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }

        public void ReturnClient(PooledHttpClient client)
        {
            if (client == null || _disposed) return;

            _activeClients.TryRemove(client.Id, out _);
            
            if (!client.IsExpired && !client.HasErrors)
            {
                client.MarkAsIdle();
                _availableClients.Enqueue(client);
            }
            else
            {
                client.Dispose();
            }

            _semaphore.Release();
        }

        private PooledHttpClient CreateNewClient()
        {
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 10,
                UseCookies = false // برای بهتر بودن عملکرد در دانلود
            };

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // تنظیمات پیش‌فرض برای دانلود
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "DownloadManagerH/1.0 (Windows NT; .NET 9.0)");

            var pooledClient = new PooledHttpClient(httpClient, _host, Interlocked.Increment(ref _totalCreated));
            
            _logger.LogDebug($"HttpClient جدید ایجاد شد برای {_host} (ID: {pooledClient.Id})");
            
            return pooledClient;
        }

        public void CleanupExpiredClients(TimeSpan idleTimeout)
        {
            var expiredClients = new List<PooledHttpClient>();
            var remainingClients = new List<PooledHttpClient>();

            // بررسی client های در دسترس
            while (_availableClients.TryDequeue(out var client))
            {
                if (client.IsExpired || client.IdleTime > idleTimeout)
                {
                    expiredClients.Add(client);
                }
                else
                {
                    remainingClients.Add(client);
                }
            }

            // بازگرداندن client های معتبر به pool
            foreach (var client in remainingClients)
            {
                _availableClients.Enqueue(client);
            }

            // dispose کردن client های منقضی شده
            foreach (var client in expiredClients)
            {
                client.Dispose();
            }

            if (expiredClients.Count > 0)
            {
                _logger.LogDebug($"{expiredClients.Count} HttpClient منقضی شده از pool {_host} حذف شد");
            }
        }

        public HttpClientPoolStatistics GetStatistics()
        {
            return new HttpClientPoolStatistics
            {
                Host = _host,
                TotalClients = _totalCreated,
                ActiveClients = _activeClients.Count,
                IdleClients = _availableClients.Count,
                LastUsed = LastUsed
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // dispose کردن تمام client های فعال
                foreach (var client in _activeClients.Values)
                {
                    client.Dispose();
                }
                _activeClients.Clear();

                // dispose کردن تمام client های در دسترس
                while (_availableClients.TryDequeue(out var client))
                {
                    client.Dispose();
                }

                _semaphore?.Dispose();
            }
        }
    }

    /// <summary>
    /// HttpClient با قابلیت‌های pool management
    /// </summary>
    public class PooledHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly DateTime _createdAt;
        private DateTime _lastUsed;
        private bool _isActive;
        private bool _hasErrors;
        private bool _disposed = false;

        public int Id { get; }
        public string PoolKey { get; }
        public HttpClient Client => _httpClient;
        
        public bool IsExpired => DateTime.Now - _createdAt > TimeSpan.FromMinutes(30);
        public bool HasErrors => _hasErrors;
        public TimeSpan IdleTime => DateTime.Now - _lastUsed;
        public bool IsActive => _isActive;

        internal PooledHttpClient(HttpClient httpClient, string poolKey, int id)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            PoolKey = poolKey;
            Id = id;
            _createdAt = DateTime.Now;
            _lastUsed = DateTime.Now;
            _isActive = true;
        }

        internal void MarkAsActive()
        {
            _isActive = true;
            _lastUsed = DateTime.Now;
        }

        internal void MarkAsIdle()
        {
            _isActive = false;
            _lastUsed = DateTime.Now;
        }

        public void ReportError()
        {
            _hasErrors = true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _httpClient?.Dispose();
            }
        }
    }

    #region Statistics Models

    public class ConnectionPoolStatistics
    {
        public int TotalPools { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        
        public double UtilizationRate => TotalConnections > 0 ? 
            (double)ActiveConnections / TotalConnections * 100 : 0;
    }

    public class HttpClientPoolStatistics
    {
        public string Host { get; set; } = "";
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
        public int IdleClients { get; set; }
        public DateTime LastUsed { get; set; }
    }

    #endregion
}