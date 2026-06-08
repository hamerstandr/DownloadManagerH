using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت منابع و الگوهای disposal مناسب
    /// </summary>
    public class ResourceManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, IDisposable> _managedResources;
        private readonly ConcurrentDictionary<string, WeakReference> _weakReferences;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _semaphore;
        private readonly List<string> _tempFiles;
        private readonly List<string> _tempDirectories;
        private bool _disposed = false;

        public event EventHandler<ResourceEventArgs> ResourceCreated;
        public event EventHandler<ResourceEventArgs> ResourceDisposed;
        public event EventHandler<ResourceCleanupEventArgs>? CleanupCompleted;

        public ResourceManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _managedResources = new ConcurrentDictionary<string, IDisposable>();
            _weakReferences = new ConcurrentDictionary<string, WeakReference>();
            _semaphore = new SemaphoreSlim(1, 1);
            _tempFiles = new List<string>();
            _tempDirectories = new List<string>();

            // تایمر پاک‌سازی هر 2 دقیقه
            _cleanupTimer = new Timer(PerformCleanup, null, 
                TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

            _logger.LogInfo("ResourceManager مقداردهی اولیه شد");
        }

        /// <summary>
        /// ثبت منبع برای مدیریت خودکار
        /// </summary>
        public void RegisterResource<T>(string key, T resource) where T : IDisposable
        {
            if (resource == null) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ResourceManager));

            _managedResources.AddOrUpdate(key, resource, (k, existing) =>
            {
                existing?.Dispose();
                return resource;
            });

            ResourceCreated?.Invoke(this, new ResourceEventArgs
            {
                ResourceKey = key,
                ResourceType = typeof(T).Name,
                Action = ResourceAction.Created
            });

            _logger.LogDebug($"منبع ثبت شد: {key} ({typeof(T).Name})");
        }

        /// <summary>
        /// ثبت weak reference برای نظارت بر منابع
        /// </summary>
        public void RegisterWeakReference(string key, object target)
        {
            if (target == null) return;
            if (_disposed) return;

            _weakReferences.AddOrUpdate(key, new WeakReference(target), 
                (k, existing) => new WeakReference(target));

            _logger.LogDebug($"Weak reference ثبت شد: {key}");
        }

        /// <summary>
        /// حذف منبع از مدیریت
        /// </summary>
        public bool UnregisterResource(string key)
        {
            if (_managedResources.TryRemove(key, out var resource))
            {
                resource?.Dispose();
                
                ResourceDisposed?.Invoke(this, new ResourceEventArgs
                {
                    ResourceKey = key,
                    ResourceType = resource?.GetType().Name ?? "Unknown",
                    Action = ResourceAction.Disposed
                });

                _logger.LogDebug($"منبع حذف شد: {key}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// دریافت منبع مدیریت شده
        /// </summary>
        public T GetResource<T>(string key) where T : class, IDisposable
        {
            if (_managedResources.TryGetValue(key, out var resource))
            {
                return resource as T;
            }
            return null;
        }

        /// <summary>
        /// ثبت فایل موقت برای پاک‌سازی خودکار
        /// </summary>
        public void RegisterTempFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            lock (_tempFiles)
            {
                if (!_tempFiles.Contains(filePath))
                {
                    _tempFiles.Add(filePath);
                    _logger.LogDebug($"فایل موقت ثبت شد: {filePath}");
                }
            }
        }

        /// <summary>
        /// ثبت دایرکتوری موقت برای پاک‌سازی خودکار
        /// </summary>
        public void RegisterTempDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;

            lock (_tempDirectories)
            {
                if (!_tempDirectories.Contains(directoryPath))
                {
                    _tempDirectories.Add(directoryPath);
                    _logger.LogDebug($"دایرکتوری موقت ثبت شد: {directoryPath}");
                }
            }
        }

        /// <summary>
        /// پاک‌سازی فایل‌های موقت
        /// </summary>
        public async Task CleanupTempFilesAsync()
        {
            var cleanedFiles = 0;
            var errors = new List<string>();

            await Task.Run(() =>
            {
                lock (_tempFiles)
                {
                    for (int i = _tempFiles.Count - 1; i >= 0; i--)
                    {
                        var filePath = _tempFiles[i];
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                                cleanedFiles++;
                            }
                            _tempFiles.RemoveAt(i);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"خطا در حذف {filePath}: {ex.Message}");
                        }
                    }
                }
            });

            if (cleanedFiles > 0)
            {
                _logger.LogInfo($"{cleanedFiles} فایل موقت پاک شد");
            }

            if (errors.Count > 0)
            {
                _logger.LogWarning($"خطا در پاک‌سازی {errors.Count} فایل موقت");
                foreach (var error in errors)
                {
                    _logger.LogDebug(error);
                }
            }
        }

        /// <summary>
        /// پاک‌سازی دایرکتوری‌های موقت
        /// </summary>
        public async Task CleanupTempDirectoriesAsync()
        {
            var cleanedDirs = 0;
            var errors = new List<string>();

            await Task.Run(() =>
            {
                lock (_tempDirectories)
                {
                    for (int i = _tempDirectories.Count - 1; i >= 0; i--)
                    {
                        var dirPath = _tempDirectories[i];
                        try
                        {
                            if (Directory.Exists(dirPath))
                            {
                                Directory.Delete(dirPath, true);
                                cleanedDirs++;
                            }
                            _tempDirectories.RemoveAt(i);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"خطا در حذف {dirPath}: {ex.Message}");
                        }
                    }
                }
            });

            if (cleanedDirs > 0)
            {
                _logger.LogInfo($"{cleanedDirs} دایرکتوری موقت پاک شد");
            }

            if (errors.Count > 0)
            {
                _logger.LogWarning($"خطا در پاک‌سازی {errors.Count} دایرکتوری موقت");
            }
        }

        /// <summary>
        /// بررسی و پاک‌سازی weak reference های مرده
        /// </summary>
        private void CleanupDeadReferences()
        {
            var deadReferences = new List<string>();

            foreach (var kvp in _weakReferences)
            {
                if (!kvp.Value.IsAlive)
                {
                    deadReferences.Add(kvp.Key);
                }
            }

            foreach (var key in deadReferences)
            {
                _weakReferences.TryRemove(key, out _);
            }

            if (deadReferences.Count > 0)
            {
                _logger.LogDebug($"{deadReferences.Count} weak reference مرده پاک شد");
            }
        }

        /// <summary>
        /// پاک‌سازی دوره‌ای منابع
        /// </summary>
        private async void PerformCleanup(object state)
        {
            if (_disposed) return;

            try
            {
                await _semaphore.WaitAsync();

                var startTime = DateTime.Now;
                var initialResourceCount = _managedResources.Count;
                var initialWeakRefCount = _weakReferences.Count;

                // پاک‌سازی weak reference های مرده
                CleanupDeadReferences();

                // پاک‌سازی فایل‌های موقت
                await CleanupTempFilesAsync();

                // پاک‌سازی دایرکتوری‌های موقت
                await CleanupTempDirectoriesAsync();

                // اجرای Garbage Collection اگر نیاز باشد
                if (ShouldRunGarbageCollection())
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                var duration = DateTime.Now - startTime;
                var finalResourceCount = _managedResources.Count;
                var finalWeakRefCount = _weakReferences.Count;

                CleanupCompleted?.Invoke(this, new ResourceCleanupEventArgs
                {
                    Duration = duration,
                    InitialResourceCount = initialResourceCount,
                    FinalResourceCount = finalResourceCount,
                    InitialWeakRefCount = initialWeakRefCount,
                    FinalWeakRefCount = finalWeakRefCount
                });

                _logger.LogDebug($"پاک‌سازی منابع تکمیل شد در {duration.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پاک‌سازی منابع", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// تشخیص نیاز به اجرای Garbage Collection
        /// </summary>
        private bool ShouldRunGarbageCollection()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var threshold = 100 * 1024 * 1024; // 100 MB

            return totalMemory > threshold;
        }

        /// <summary>
        /// دریافت آمار منابع
        /// </summary>
        public ResourceStatistics GetStatistics()
        {
            var aliveWeakRefs = _weakReferences.Values.Count(wr => wr.IsAlive);
            var deadWeakRefs = _weakReferences.Count - aliveWeakRefs;

            return new ResourceStatistics
            {
                ManagedResourceCount = _managedResources.Count,
                WeakReferenceCount = _weakReferences.Count,
                AliveWeakReferences = aliveWeakRefs,
                DeadWeakReferences = deadWeakRefs,
                TempFileCount = _tempFiles.Count,
                TempDirectoryCount = _tempDirectories.Count,
                TotalMemoryUsage = GC.GetTotalMemory(false)
            };
        }

        /// <summary>
        /// اجرای پاک‌سازی فوری
        /// </summary>
        public async Task ForceCleanupAsync()
        {
            if (_disposed) return;

            try
            {
                await _semaphore.WaitAsync();
                await PerformCleanupInternal();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task PerformCleanupInternal()
        {
            CleanupDeadReferences();
            await CleanupTempFilesAsync();
            await CleanupTempDirectoriesAsync();
            
            // اجرای Garbage Collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _cleanupTimer?.Dispose();

                // dispose کردن تمام منابع مدیریت شده
                foreach (var resource in _managedResources.Values)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"خطا در dispose کردن منبع: {ex.Message}");
                    }
                }
                _managedResources.Clear();

                // پاک‌سازی فایل‌ها و دایرکتوری‌های موقت
                Task.Run(async () =>
                {
                    try
                    {
                        await CleanupTempFilesAsync();
                        await CleanupTempDirectoriesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"خطا در پاک‌سازی نهایی: {ex.Message}");
                    }
                });

                _semaphore?.Dispose();
                _logger.LogInfo("ResourceManager disposed");
            }
        }
    }

    #region Event Args and Data Models

    public class ResourceEventArgs : EventArgs
    {
        public string ResourceKey { get; set; }
        public string ResourceType { get; set; }
        public ResourceAction Action { get; set; }
    }

    public class ResourceCleanupEventArgs : EventArgs
    {
        public TimeSpan Duration { get; set; }
        public int InitialResourceCount { get; set; }
        public int FinalResourceCount { get; set; }
        public int InitialWeakRefCount { get; set; }
        public int FinalWeakRefCount { get; set; }
    }

    public class ResourceStatistics
    {
        public int ManagedResourceCount { get; set; }
        public int WeakReferenceCount { get; set; }
        public int AliveWeakReferences { get; set; }
        public int DeadWeakReferences { get; set; }
        public int TempFileCount { get; set; }
        public int TempDirectoryCount { get; set; }
        public long TotalMemoryUsage { get; set; }

        public string TotalMemoryUsageFormatted => FormatBytes(TotalMemoryUsage);

        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:F2} {sizes[order]}";
        }
    }

    public enum ResourceAction
    {
        Created,
        Disposed,
        Accessed,
        Cleaned
    }

    #endregion
}