using System;
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
    /// نظارت بر حافظه و مکانیزم‌های پاک‌سازی
    /// </summary>
    public class MemoryMonitor : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Timer _monitoringTimer;
        private readonly Process _currentProcess;
        private readonly List<MemorySnapshot> _snapshots;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed = false;

        // آستانه‌های هشدار (بایت)
        public long WarningThreshold { get; set; } = 500 * 1024 * 1024; // 500 MB
        public long CriticalThreshold { get; set; } = 1024 * 1024 * 1024; // 1 GB
        public long MaxSnapshotHistory { get; set; } = 100;

        // تنظیمات نظارت
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);
        public bool AutoCleanupEnabled { get; set; } = true;
        public bool DetailedLoggingEnabled { get; set; } = false;

        public event EventHandler<MemoryPressureEventArgs> MemoryPressureDetected;
        public event EventHandler<MemoryLeakEventArgs> PotentialMemoryLeakDetected;
        public event EventHandler<MemoryCleanupEventArgs> CleanupPerformed;

        public MemoryMonitor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentProcess = Process.GetCurrentProcess();
            _snapshots = new List<MemorySnapshot>();
            _semaphore = new SemaphoreSlim(1, 1);

            _monitoringTimer = new Timer(MonitoringInterval.TotalMilliseconds);
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.Start();

            _logger.LogInfo("MemoryMonitor شروع به کار کرد");
        }

        /// <summary>
        /// گرفتن snapshot از وضعیت فعلی حافظه
        /// </summary>
        public MemorySnapshot TakeSnapshot()
        {
            try
            {
                _currentProcess.Refresh();

                var snapshot = new MemorySnapshot
                {
                    Timestamp = DateTime.Now,
                    WorkingSet = _currentProcess.WorkingSet64,
                    PrivateMemorySize = _currentProcess.PrivateMemorySize64,
                    VirtualMemorySize = _currentProcess.VirtualMemorySize64,
                    PagedMemorySize = _currentProcess.PagedMemorySize64,
                    NonPagedSystemMemorySize = _currentProcess.NonpagedSystemMemorySize64,
                    PagedSystemMemorySize = _currentProcess.PagedSystemMemorySize64,
                    GCTotalMemory = GC.GetTotalMemory(false),
                    GCGen0Collections = GC.CollectionCount(0),
                    GCGen1Collections = GC.CollectionCount(1),
                    GCGen2Collections = GC.CollectionCount(2),
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount
                };

                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در گرفتن memory snapshot", ex);
                return null;
            }
        }

        /// <summary>
        /// تحلیل روند استفاده از حافظه
        /// </summary>
        public MemoryTrend AnalyzeMemoryTrend()
        {
            if (_snapshots.Count < 3)
                return MemoryTrend.Stable;

            var recentSnapshots = _snapshots.TakeLast(10).ToList();
            var workingSetTrend = CalculateTrend(recentSnapshots.Select(s => (double)s.WorkingSet));
            var gcMemoryTrend = CalculateTrend(recentSnapshots.Select(s => (double)s.GCTotalMemory));

            // اگر هر دو روند صعودی باشند
            if (workingSetTrend > 0.1 && gcMemoryTrend > 0.1)
                return MemoryTrend.Increasing;
            
            // اگر هر دو روند نزولی باشند
            if (workingSetTrend < -0.1 && gcMemoryTrend < -0.1)
                return MemoryTrend.Decreasing;

            return MemoryTrend.Stable;
        }

        /// <summary>
        /// محاسبه روند (slope) یک سری داده
        /// </summary>
        private double CalculateTrend(IEnumerable<double> values)
        {
            var valueList = values.ToList();
            if (valueList.Count < 2) return 0;

            var n = valueList.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += valueList[i];
                sumXY += i * valueList[i];
                sumX2 += i * i;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        /// <summary>
        /// تشخیص نشت حافظه احتمالی
        /// </summary>
        private void DetectMemoryLeak()
        {
            if (_snapshots.Count < 20) return;

            var recentSnapshots = _snapshots.TakeLast(20).ToList();
            var oldestSnapshot = recentSnapshots.First();
            var newestSnapshot = recentSnapshots.Last();

            // بررسی افزایش مداوم حافظه
            var memoryIncrease = newestSnapshot.WorkingSet - oldestSnapshot.WorkingSet;
            var timeSpan = newestSnapshot.Timestamp - oldestSnapshot.Timestamp;
            var increaseRate = memoryIncrease / timeSpan.TotalMinutes; // بایت در دقیقه

            // اگر حافظه بیش از 10 MB در دقیقه افزایش یابد
            if (increaseRate > 10 * 1024 * 1024)
            {
                var leakEvent = new MemoryLeakEventArgs
                {
                    MemoryIncreaseRate = increaseRate,
                    TimeSpan = timeSpan,
                    TotalIncrease = memoryIncrease,
                    CurrentMemoryUsage = newestSnapshot.WorkingSet
                };

                PotentialMemoryLeakDetected?.Invoke(this, leakEvent);
                
                _logger.LogWarning($"نشت حافظه احتمالی تشخیص داده شد: {FormatBytes(memoryIncrease)} در {timeSpan.TotalMinutes:F1} دقیقه");
            }
        }

        /// <summary>
        /// اجرای پاک‌سازی حافظه
        /// </summary>
        public async Task<MemoryCleanupResult> PerformCleanupAsync()
        {
            var beforeSnapshot = TakeSnapshot();
            var startTime = DateTime.Now;

            try
            {
                await _semaphore.WaitAsync();

                // مرحله 1: اجرای Garbage Collection
                GC.Collect(0, GCCollectionMode.Optimized);
                await Task.Delay(100);

                GC.Collect(1, GCCollectionMode.Optimized);
                await Task.Delay(100);

                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // مرحله 2: فشرده‌سازی Large Object Heap
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();

                // مرحله 3: تنظیم working set
                await Task.Run(() =>
                {
                    try
                    {
                        // تلاش برای کاهش working set
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        
                        // استفاده از Windows API برای تنظیم working set
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            SetProcessWorkingSetSize(_currentProcess.Handle, -1, -1);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"خطا در تنظیم working set: {ex.Message}");
                    }
                });

                var afterSnapshot = TakeSnapshot();
                var duration = DateTime.Now - startTime;

                var result = new MemoryCleanupResult
                {
                    Success = true,
                    Duration = duration,
                    MemoryFreed = beforeSnapshot.WorkingSet - afterSnapshot.WorkingSet,
                    GCMemoryFreed = beforeSnapshot.GCTotalMemory - afterSnapshot.GCTotalMemory,
                    BeforeSnapshot = beforeSnapshot,
                    AfterSnapshot = afterSnapshot
                };

                CleanupPerformed?.Invoke(this, new MemoryCleanupEventArgs { Result = result });

                _logger.LogInfo($"پاک‌سازی حافظه تکمیل شد: {FormatBytes(result.MemoryFreed)} آزاد شد در {duration.TotalMilliseconds:F0}ms");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پاک‌سازی حافظه", ex);
                return new MemoryCleanupResult { Success = false, Duration = DateTime.Now - startTime };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// دریافت آمار حافظه
        /// </summary>
        public MemoryStatistics GetStatistics()
        {
            var currentSnapshot = TakeSnapshot();
            var trend = AnalyzeMemoryTrend();

            var stats = new MemoryStatistics
            {
                CurrentSnapshot = currentSnapshot,
                MemoryTrend = trend,
                SnapshotCount = _snapshots.Count,
                MonitoringDuration = _snapshots.Count > 0 ? 
                    DateTime.Now - _snapshots.First().Timestamp : TimeSpan.Zero
            };

            if (_snapshots.Count > 1)
            {
                var firstSnapshot = _snapshots.First();
                stats.TotalMemoryChange = currentSnapshot.WorkingSet - firstSnapshot.WorkingSet;
                stats.AverageMemoryUsage = (long)_snapshots.Average(s => s.WorkingSet);
                stats.PeakMemoryUsage = _snapshots.Max(s => s.WorkingSet);
                stats.MinMemoryUsage = _snapshots.Min(s => s.WorkingSet);
            }

            return stats;
        }

        private void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var snapshot = TakeSnapshot();
                if (snapshot == null) return;

                // اضافه کردن snapshot به تاریخچه
                _snapshots.Add(snapshot);

                // حذف snapshot های قدیمی
                if (_snapshots.Count > MaxSnapshotHistory)
                {
                    _snapshots.RemoveAt(0);
                }

                // بررسی فشار حافظه
                CheckMemoryPressure(snapshot);

                // تشخیص نشت حافظه
                DetectMemoryLeak();

                // پاک‌سازی خودکار در صورت نیاز
                if (AutoCleanupEnabled && snapshot.WorkingSet > CriticalThreshold)
                {
                    _ = Task.Run(async () => await PerformCleanupAsync());
                }

                // لاگ تفصیلی
                if (DetailedLoggingEnabled)
                {
                    _logger.LogDebug($"Memory: {FormatBytes(snapshot.WorkingSet)}, GC: {FormatBytes(snapshot.GCTotalMemory)}, Threads: {snapshot.ThreadCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در نظارت حافظه", ex);
            }
        }

        private void CheckMemoryPressure(MemorySnapshot snapshot)
        {
            MemoryPressureLevel level = MemoryPressureLevel.Normal;
            
            if (snapshot.WorkingSet > CriticalThreshold)
                level = MemoryPressureLevel.Critical;
            else if (snapshot.WorkingSet > WarningThreshold)
                level = MemoryPressureLevel.High;

            if (level != MemoryPressureLevel.Normal)
            {
                var eventArgs = new MemoryPressureEventArgs
                {
                    Level = level,
                    CurrentMemoryUsage = snapshot.WorkingSet,
                    Threshold = level == MemoryPressureLevel.Critical ? CriticalThreshold : WarningThreshold,
                    Snapshot = snapshot
                };

                MemoryPressureDetected?.Invoke(this, eventArgs);

                var levelText = level == MemoryPressureLevel.Critical ? "بحرانی" : "بالا";
                _logger.LogWarning($"فشار حافظه {levelText}: {FormatBytes(snapshot.WorkingSet)}");
            }
        }

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

        // Windows API برای تنظیم working set
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int? dwMinimumWorkingSetSize, int? dwMaximumWorkingSetSize);

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _monitoringTimer?.Stop();
                _monitoringTimer?.Dispose();
                _semaphore?.Dispose();
                _logger.LogInfo("MemoryMonitor disposed");
            }
        }
    }

    #region Data Models and Enums

    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public long WorkingSet { get; set; }
        public long PrivateMemorySize { get; set; }
        public long VirtualMemorySize { get; set; }
        public long PagedMemorySize { get; set; }
        public long NonPagedSystemMemorySize { get; set; }
        public long PagedSystemMemorySize { get; set; }
        public long GCTotalMemory { get; set; }
        public int GCGen0Collections { get; set; }
        public int GCGen1Collections { get; set; }
        public int GCGen2Collections { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }

        public string WorkingSetFormatted => FormatBytes(WorkingSet);
        public string GCTotalMemoryFormatted => FormatBytes(GCTotalMemory);

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

    public class MemoryStatistics
    {
        public MemorySnapshot CurrentSnapshot { get; set; }
        public MemoryTrend MemoryTrend { get; set; }
        public int SnapshotCount { get; set; }
        public TimeSpan MonitoringDuration { get; set; }
        public long TotalMemoryChange { get; set; }
        public long AverageMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public long MinMemoryUsage { get; set; }
    }

    public class MemoryCleanupResult
    {
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public long MemoryFreed { get; set; }
        public long GCMemoryFreed { get; set; }
        public MemorySnapshot BeforeSnapshot { get; set; }
        public MemorySnapshot AfterSnapshot { get; set; }
    }

    public class MemoryPressureEventArgs : EventArgs
    {
        public MemoryPressureLevel Level { get; set; }
        public long CurrentMemoryUsage { get; set; }
        public long Threshold { get; set; }
        public MemorySnapshot? Snapshot { get; set; }
    }

    public class MemoryLeakEventArgs : EventArgs
    {
        public double MemoryIncreaseRate { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public long TotalIncrease { get; set; }
        public long CurrentMemoryUsage { get; set; }
    }

    public class MemoryCleanupEventArgs : EventArgs
    {
        public MemoryCleanupResult? Result { get; set; }
    }

    public enum MemoryTrend
    {
        Stable,
        Increasing,
        Decreasing
    }

    public enum MemoryPressureLevel
    {
        Normal,
        High,
        Critical
    }

    #endregion
}