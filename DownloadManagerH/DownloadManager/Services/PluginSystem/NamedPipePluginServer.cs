using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Services.PluginSystem
{
    /// <summary>
    /// سرور Named Pipe برای ارتباط با افزونه‌های DownloadManager
    /// امکان دریافت داده‌های استریم از برنامه‌های شخص ثالث را فراهم می‌کند
    /// </summary>
    public class NamedPipePluginServer : IDisposable
    {
        private const string PipeName = "TrafficWatchPluginPipe";
        private readonly ILogger _logger;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serverTask;
        private bool _isRunning;
        private bool _disposed;

        // لیست افزونه‌های متصل
        private readonly ConcurrentDictionary<string, PluginConnection> _connectedPlugins;

        // رویدادها
        public event EventHandler<PluginRegisteredEventArgs>? PluginRegistered;
        public event EventHandler<PluginDataReceivedEventArgs>? DataReceived;
        public event EventHandler<PluginDisconnectedEventArgs>? PluginDisconnected;

        public NamedPipePluginServer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectedPlugins = new ConcurrentDictionary<string, PluginConnection>();
        }

        /// <summary>
        /// شروع سرور Named Pipe
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Named Pipe server is already running");
                return;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _serverTask = Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
                _isRunning = true;
                _logger.LogInfo($"Named Pipe server started on pipe: {PipeName}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start Named Pipe server", ex);
                throw;
            }
        }

        /// <summary>
        /// توقف سرور Named Pipe
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _serverTask?.Wait(TimeSpan.FromSeconds(5));
                _isRunning = false;

                // قطع اتصال همه کلاینت‌ها
                foreach (var plugin in _connectedPlugins.Values)
                {
                    plugin.Dispose();
                }
                _connectedPlugins.Clear();

                _logger.LogInfo("Named Pipe server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping Named Pipe server", ex);
            }
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    _logger.LogDebug("Waiting for plugin connection...");

                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    _ = Task.Run(() => HandleClientAsync(pipeServer, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Server cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError("Error in Named Pipe server loop", ex);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
        {
            var connection = new PluginConnection(pipeServer, _logger);
            string pluginId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                _logger.LogInfo($"Plugin connected: {pluginId}");

                while (!cancellationToken.IsCancellationRequested && pipeServer.IsConnected)
                {
                    var message = await connection.ReadMessageAsync(cancellationToken);

                    if (message == null)
                    {
                        _logger.LogDebug($"Empty message from {pluginId}");
                        continue;
                    }

                    await ProcessMessageAsync(pluginId, message, connection);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Connection cancelled for {pluginId}");
            }
            catch (IOException ex) when (ex.InnerException is OperationCanceledException)
            {
                _logger.LogDebug($"Connection cancelled for {pluginId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling client {pluginId}", ex);
            }
            finally
            {
                // حذف افزونه از لیست
                if (_connectedPlugins.TryRemove(pluginId, out _))
                {
                    PluginDisconnected?.Invoke(this, new PluginDisconnectedEventArgs(pluginId));
                    _logger.LogInfo($"Plugin disconnected: {pluginId}");
                }

                connection.Dispose();
            }
        }

        private async Task ProcessMessageAsync(string pluginId, JsonElement message, PluginConnection connection)
        {
            try
            {
                if (!message.TryGetProperty("action", out var actionElement))
                {
                    _logger.LogWarning($"Message without action from {pluginId}");
                    return;
                }

                var action = actionElement.GetString()?.ToLower();

                switch (action)
                {
                    case "register":
                        await HandleRegisterAsync(pluginId, message, connection);
                        break;

                    case "stream_data":
                        await HandleStreamDataAsync(pluginId, message);
                        break;

                    case "heartbeat":
                        await HandleHeartbeatAsync(pluginId, connection);
                        break;

                    default:
                        _logger.LogWarning($"Unknown action '{action}' from {pluginId}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message from {pluginId}", ex);
            }
        }

        private async Task HandleRegisterAsync(string pluginId, JsonElement message, PluginConnection connection)
        {
            var name = message.GetProperty("name").GetString() ?? "Unknown Plugin";
            var version = message.GetProperty("version").GetString() ?? "1.0.0";
            var icon = message.GetProperty("icon").GetString() ?? "📦";

            var plugin = new PluginInfo
            {
                Id = pluginId,
                Name = name,
                Version = version,
                Icon = icon,
                ConnectedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            _connectedPlugins[pluginId] = connection;
            connection.PluginInfo = plugin;

            _logger.LogInfo($"Plugin registered: {name} v{version} ({pluginId})");

            // ارسال پاسخ ثبت نام
            var response = new
            {
                action = "registered",
                id = pluginId,
                status = "success"
            };

            await connection.SendMessageAsync(response);

            // اطلاع‌رسانی رویداد
            PluginRegistered?.Invoke(this, new PluginRegisteredEventArgs(plugin));
        }

        private Task HandleStreamDataAsync(string pluginId, JsonElement message)
        {
            if (!_connectedPlugins.ContainsKey(pluginId))
            {
                _logger.LogWarning($"Stream data from unregistered plugin: {pluginId}");
                return Task.CompletedTask;
            }

            var timestamp = message.GetProperty("timestamp").GetDateTime();
            var payload = message.GetProperty("payload");

            _logger.LogDebug($"Stream data received from {pluginId}: {timestamp}");

            // اطلاع‌رسانی رویداد به داشبورد
            DataReceived?.Invoke(this, new PluginDataReceivedEventArgs(
                pluginId,
                payload.GetRawText(),
                timestamp));

            return Task.CompletedTask;
        }

        private async Task HandleHeartbeatAsync(string pluginId, PluginConnection connection)
        {
            if (_connectedPlugins.TryGetValue(pluginId, out var conn) && conn.PluginInfo != null)
            {
                conn.PluginInfo.LastHeartbeat = DateTime.UtcNow;
            }

            // ارسال پاسخ heartbeat
            var response = new { action = "heartbeat_ack", timestamp = DateTime.UtcNow };
            await connection.SendMessageAsync(response);
        }

        /// <summary>
        /// ارسال پیام به یک افزونه خاص
        /// </summary>
        public async Task SendToPluginAsync(string pluginId, object message)
        {
            if (_connectedPlugins.TryGetValue(pluginId, out var connection))
            {
                await connection.SendMessageAsync(message);
            }
            else
            {
                _logger.LogWarning($"Cannot send to unknown plugin: {pluginId}");
            }
        }

        /// <summary>
        /// ارسال پیام به همه افزونه‌های متصل
        /// </summary>
        public async Task BroadcastAsync(object message)
        {
            foreach (var connection in _connectedPlugins.Values)
            {
                try
                {
                    await connection.SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error broadcasting message", ex);
                }
            }
        }

        /// <summary>
        /// دریافت اطلاعات همه افزونه‌های متصل
        /// </summary>
        public PluginInfo[] GetConnectedPlugins()
        {
            return _connectedPlugins
                .Where(kvp => kvp.Value.PluginInfo != null)
                .Select(kvp => kvp.Value.PluginInfo!)
                .ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Stop();
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    /// <summary>
    /// نمایانگر یک اتصال افزونه
    /// </summary>
    public class PluginConnection : IDisposable
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly ILogger _logger;
        private bool _disposed;

        public PluginInfo? PluginInfo { get; set; }

        public PluginConnection(NamedPipeServerStream pipe, ILogger logger)
        {
            _pipe = pipe;
            _logger = logger;
        }

        public async Task<JsonElement?> ReadMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[4096];
                var bytesRead = await _pipe.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead == 0)
                {
                    return null;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return JsonDocument.Parse(json).RootElement;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error reading message: {ex.Message}");
                return null;
            }
        }

        public async Task SendMessageAsync(object message)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                var json = JsonSerializer.Serialize(message, options);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _pipe.WriteAsync(bytes, 0, bytes.Length);
                await _pipe.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending message", ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _pipe?.Dispose();
            }
        }
    }

    /// <summary>
    /// اطلاعات یک افزونه
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public bool IsAlive => (DateTime.UtcNow - LastHeartbeat).TotalSeconds < 60;
    }

    /// <summary>
    /// آرگومان‌های رویداد ثبت نام افزونه
    /// </summary>
    public class PluginRegisteredEventArgs : EventArgs
    {
        public PluginInfo Plugin { get; }

        public PluginRegisteredEventArgs(PluginInfo plugin)
        {
            Plugin = plugin;
        }
    }

    /// <summary>
    /// آرگومان‌های رویداد دریافت داده از افزونه
    /// </summary>
    public class PluginDataReceivedEventArgs : EventArgs
    {
        public string AddonId { get; }
        public string Data { get; }
        public DateTime Timestamp { get; }

        public PluginDataReceivedEventArgs(string addonId, string data, DateTime timestamp)
        {
            AddonId = addonId;
            Data = data;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// آرگومان‌های رویداد قطع اتصال افزونه
    /// </summary>
    public class PluginDisconnectedEventArgs : EventArgs
    {
        public string PluginId { get; }

        public PluginDisconnectedEventArgs(string pluginId)
        {
            PluginId = pluginId;
        }
    }
}
