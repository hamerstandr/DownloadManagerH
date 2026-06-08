using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using DownloadManagerH.Models.Logging;
using Timer = System.Timers.Timer;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت عملکرد و نظارت بر منابع سیستم
    /// </summary>
    public class PerformanceManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _memoryCounter;
        private readonly Process _currentProcess;
        private readonly ObjectPool<byte[]> _bufferPool;
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private bool _disposed = false;

        public event EventHandler<PerformanceEventArgs>? PerformanceAlert;
        public event EventHandler<PerformanceMemoryPressureEventArgs>? MemoryPressure;

        // آستانه‌های هشدار
        public double CpuThreshold { get; set; } = 80.0; // درصد
        public long MemoryThreshold { get; set; } = 500 * 1024 * 1024; // 500 MB
        public double DiskSpaceThreshold { get; set; } = 1024 * 1024 * 1024; // 1 GB

        public PerformanceManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentProcess = Process.GetCurrentProcess();
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            
            // ایجاد object pool برای بافرها
            _bufferPool = new ObjectPool<byte[]>(() => new byte[8192], 50);

            try
            {
                // ایجاد performance counter ها
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                // خواندن اولیه برای مقداردهی
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("نتوانست performance counter ها را ایجاد کند", ex);
            }

            // تنظیم تایمر نظارت
            _monitoringTimer = new Timer(5000); // هر 5 ثانیه
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.Start();

            _logger.LogInfo("PerformanceManager مقداردهی اولیه شد");
        }

        /// <summary>
        /// دریافت بافر از pool
        /// </summary>
        public byte[] GetBuffer()
        {
            return _bufferPool.Get();
        }

        /// <summary>
        /// بازگرداندن بافر به pool
        /// </summary>
        public void ReturnBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length == 8192)
            {
                _bufferPool.Return(buffer);
            }
        }

        /// <summary>
        /// شروع اندازه‌گیری عملکرد یک عملیات
        /// </summary>
        public PerformanceTracker StartTracking(string operationName)
        {
            return new PerformanceTracker(operationName, this);
        }

        /// <summary>
        /// ثبت معیار عملکرد
        /// </summary>
        internal void RecordMetric(string name, TimeSpan duration, long memoryUsed = 0)
        {
            _metrics.AddOrUpdate(name, 
                new PerformanceMetric(name, duration, memoryUsed),
                (key, existing) => existing.Update(duration, memoryUsed));
        }

        /// <summary>
        /// دریافت آمار عملکرد
        /// </summary>
        public PerformanceStatistics GetStatistics()
        {
            var stats = new PerformanceStatistics
            {
                ProcessMemoryUsage = _currentProcess.WorkingSet64,
                ProcessCpuTime = _currentProcess.TotalProcessorTime,
                ThreadCount = _currentProcess.Threads.Count,
                HandleCount = _currentProcess.HandleCount,
                GCTotalMemory = GC.GetTotalMemory(false),
                GCGen0Collections = GC.CollectionCount(0),
                GCGen1Collections = GC.CollectionCount(1),
                GCGen2Collections = GC.CollectionCount(2),
                OperationMetrics = _metrics.Values.ToList()
            };

            try
            {
                stats.SystemCpuUsage = _cpuCounter?.NextValue() ?? 0;
                stats.AvailableMemory = (long)((_memoryCounter?.NextValue() ?? 0) * 1024 * 1024); // MB to bytes
            }
            catch (Exception ex)
            {
                _logger.LogWarning("خطا در دریافت آمار سیستم", ex);
            }

            return stats;
        }

        /// <summary>
        /// بهینه‌سازی حافظه
        /// </summary>
        public void OptimizeMemory()
        {
            try
            {
                // اجرای Garbage Collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // فشرده‌سازی Large Object Heap
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();

                // پاک‌سازی object pool
                _bufferPool.Clear();

                _logger.LogInfo("بهینه‌سازی حافظه انجام شد");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بهینه‌سازی حافظه", ex);
            }
        }

        /// <summary>
        /// تنظیم اولویت پردازش
        /// </summary>
        public void SetProcessPriority(ProcessPriorityClass priority)
        {
            try
            {
                _currentProcess.PriorityClass = priority;
                _logger.LogInfo($"اولویت پردازش تنظیم شد: {priority}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("نتوانست اولویت پردازش را تنظیم کند", ex);
            }
        }

        /// <summary>
        /// نظارت بر فشار حافظه
        /// </summary>
        private void MonitorMemoryPressure()
        {
            var memoryUsage = _currentProcess.WorkingSet64;
            var gcMemory = GC.GetTotalMemory(false);

            if (memoryUsage > MemoryThreshold)
            {
                var args = new PerformanceMemoryPressureEventArgs
                {
                    ProcessMemoryUsage = memoryUsage,
                    GCMemoryUsage = gcMemory,
                    Severity = memoryUsage > MemoryThreshold * 2 ? 
                              MemoryPressureSeverity.High : MemoryPressureSeverity.Medium
                };

                MemoryPressure?.Invoke(this, args);

                // بهینه‌سازی خودکار در صورت فشار بالا
                if (args.Severity == MemoryPressureSeverity.High)
                {
                    Task.Run(() => OptimizeMemory());
                }
            }
        }

        private void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // نظارت بر فشار حافظه
                MonitorMemoryPressure();

                // نظارت بر CPU
                var cpuUsage = _cpuCounter?.NextValue() ?? 0;
                if (cpuUsage > CpuThreshold)
                {
                    PerformanceAlert?.Invoke(this, new PerformanceEventArgs
                    {
                        AlertType = PerformanceAlertType.HighCpuUsage,
                        Value = cpuUsage,
                        Message = $"استفاده از CPU بالا است: {cpuUsage:F1}%"
                    });
                }

                // پاک‌سازی معیارهای قدیمی
                CleanupOldMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در نظارت عملکرد", ex);
            }
        }

        private void CleanupOldMetrics()
        {
            var cutoffTime = DateTime.Now.AddHours(-1);
            var keysToRemove = _metrics
                .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _metrics.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Stop();
                _monitoringTimer?.Dispose();
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _bufferPool?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Object Pool برای مدیریت بهتر حافظه
    /// </summary>
    public class ObjectPool<T> : IDisposable where T : class
    {
        private readonly ConcurrentQueue<T> _objects = new();
        private readonly Func<T> _objectGenerator;
        private readonly int _maxSize;
        private int _currentCount = 0;

        public ObjectPool(Func<T> objectGenerator, int maxSize = 100)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _maxSize = maxSize;
        }

        public T Get()
        {
            if (_objects.TryDequeue(out T? item))
            {
                Interlocked.Decrement(ref _currentCount);
                return item;
            }
            return _objectGenerator();
        }

        public void Return(T item)
        {
            if (item != null && _currentCount < _maxSize)
            {
                _objects.Enqueue(item);
                Interlocked.Increment(ref _currentCount);
            }
        }

        public void Clear()
        {
            while (_objects.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _currentCount);
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// ردیابی عملکرد عملیات
    /// </summary>
    public class PerformanceTracker : IDisposable
    {
        private readonly string _operationName;
        private readonly PerformanceManager _manager;
        private readonly Stopwatch _stopwatch;
        private readonly long _initialMemory;

        internal PerformanceTracker(string operationName, PerformanceManager manager)
        {
            _operationName = operationName;
            _manager = manager;
            _stopwatch = Stopwatch.StartNew();
            _initialMemory = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = Math.Max(0, finalMemory - _initialMemory);
            
            _manager.RecordMetric(_operationName, _stopwatch.Elapsed, memoryUsed);
        }
    }

    #region Data Models

    public class PerformanceStatistics
    {
        public long ProcessMemoryUsage { get; set; }
        public TimeSpan ProcessCpuTime { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long GCTotalMemory { get; set; }
        public int GCGen0Collections { get; set; }
        public int GCGen1Collections { get; set; }
        public int GCGen2Collections { get; set; }
        public double SystemCpuUsage { get; set; }
        public long AvailableMemory { get; set; }
        public List<PerformanceMetric> OperationMetrics { get; set; } = new();

        public string ProcessMemoryUsageFormatted => FormatBytes(ProcessMemoryUsage);
        public string GCTotalMemoryFormatted => FormatBytes(GCTotalMemory);
        public string AvailableMemoryFormatted => FormatBytes(AvailableMemory);

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

    public class PerformanceMetric
    {
        public string Name { get; set; }
        public TimeSpan AverageDuration { get; private set; }
        public TimeSpan MinDuration { get; private set; }
        public TimeSpan MaxDuration { get; private set; }
        public long AverageMemoryUsage { get; private set; }
        public int CallCount { get; private set; }
        public DateTime LastUpdated { get; private set; }

        public PerformanceMetric(string name, TimeSpan duration, long memoryUsage)
        {
            Name = name;
            AverageDuration = duration;
            MinDuration = duration;
            MaxDuration = duration;
            AverageMemoryUsage = memoryUsage;
            CallCount = 1;
            LastUpdated = DateTime.Now;
        }

        public PerformanceMetric Update(TimeSpan duration, long memoryUsage)
        {
            CallCount++;
            
            // محاسبه میانگین جدید
            var totalTicks = AverageDuration.Ticks * (CallCount - 1) + duration.Ticks;
            AverageDuration = new TimeSpan(totalTicks / CallCount);
            
            var totalMemory = AverageMemoryUsage * (CallCount - 1) + memoryUsage;
            AverageMemoryUsage = totalMemory / CallCount;
            
            // به‌روزرسانی حداقل و حداکثر
            if (duration < MinDuration) MinDuration = duration;
            if (duration > MaxDuration) MaxDuration = duration;
            
            LastUpdated = DateTime.Now;
            return this;
        }
    }

    public class PerformanceEventArgs : EventArgs
    {
        public PerformanceAlertType AlertType { get; set; }
        public double Value { get; set; }
        public string Message { get; set; } = "";
    }

    public class PerformanceMemoryPressureEventArgs : EventArgs
    {
        public long ProcessMemoryUsage { get; set; }
        public long GCMemoryUsage { get; set; }
        public MemoryPressureSeverity Severity { get; set; }
    }

    public enum PerformanceAlertType
    {
        HighCpuUsage,
        HighMemoryUsage,
        LowDiskSpace,
        SlowOperation
    }

    public enum MemoryPressureSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}