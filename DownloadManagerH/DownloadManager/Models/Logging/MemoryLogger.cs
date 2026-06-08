using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// پیاده‌سازی لاگر حافظه‌ای برای نمایش لاگ‌های زنده
    /// </summary>
    public class MemoryLogger : ILogger
    {
        private readonly LoggingConfiguration _config;
        private readonly ConcurrentQueue<LogEntry> _logEntries;
        private readonly object _lockObject = new object();

        /// <summary>
        /// رویداد اضافه شدن لاگ جدید
        /// </summary>
        public event EventHandler<LogEntry>? LogAdded;

        /// <summary>
        /// سازنده MemoryLogger
        /// </summary>
        /// <param name="config">تنظیمات لاگ‌گیری</param>
        public MemoryLogger(LoggingConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logEntries = new ConcurrentQueue<LogEntry>();
        }

        /// <summary>
        /// ثبت پیام اشکال‌زدایی
        /// </summary>
        public void LogDebug(string message, object? context = null)
        {
            AddLogEntry(LogLevel.Debug, message, "Debug", context);
        }

        /// <summary>
        /// ثبت پیام اطلاعاتی
        /// </summary>
        public void LogInfo(string message, object? context = null)
        {
            AddLogEntry(LogLevel.Info, message, "Info", context);
        }

        /// <summary>
        /// ثبت پیام هشدار
        /// </summary>
        public void LogWarning(string message, object? context = null)
        {
            AddLogEntry(LogLevel.Warning, message, "Warning", context);
        }

        /// <summary>
        /// ثبت پیام خطا
        /// </summary>
        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            var entry = new LogEntry(LogLevel.Error, message, "Error")
            {
                Exception = exception
            };
            entry.SetContext(context);
            
            AddLogEntry(entry);
        }

        /// <summary>
        /// ثبت رویداد دانلود
        /// </summary>
        public void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null)
        {
            if (!_config.EnableDownloadEventLogging)
                return;

            var contextData = new
            {
                DownloadId = item?.Url?.GetHashCode(),
                FileName = item?.FileName,
                Status = item?.Status.ToString(),
                Progress = item?.Progress,
                EventType = eventType,
                AdditionalData = additionalData
            };

            AddLogEntry(LogLevel.Info, $"رویداد دانلود: {eventType}", "Download", contextData);
        }

        /// <summary>
        /// دریافت لاگ‌های حافظه
        /// </summary>
        public Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            var filteredLogs = _logEntries
                .Where(entry => entry.Level >= minLevel)
                .Where(entry => !from.HasValue || entry.Timestamp >= from.Value)
                .Where(entry => !to.HasValue || entry.Timestamp <= to.Value)
                .OrderBy(entry => entry.Timestamp)
                .ToList();

            return Task.FromResult<IEnumerable<LogEntry>>(filteredLogs);
        }

        /// <summary>
        /// دریافت آخرین لاگ‌ها
        /// </summary>
        /// <param name="count">تعداد لاگ‌های مورد نظر</param>
        /// <returns>لیست آخرین لاگ‌ها</returns>
        public IEnumerable<LogEntry> GetRecentLogs(int count = 100)
        {
            return _logEntries
                .TakeLast(count)
                .OrderBy(entry => entry.Timestamp);
        }

        /// <summary>
        /// دریافت تعداد کل لاگ‌ها
        /// </summary>
        public int Count => _logEntries.Count;

        /// <summary>
        /// پاک کردن تمام لاگ‌های حافظه
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                while (_logEntries.TryDequeue(out _))
                {
                    // خالی کردن صف
                }
            }
        }

        /// <summary>
        /// دریافت آمار لاگ‌ها بر اساس سطح
        /// </summary>
        /// <returns>دیکشنری آمار</returns>
        public Dictionary<LogLevel, int> GetLogStatistics()
        {
            var stats = new Dictionary<LogLevel, int>
            {
                { LogLevel.Debug, 0 },
                { LogLevel.Info, 0 },
                { LogLevel.Warning, 0 },
                { LogLevel.Error, 0 }
            };

            foreach (var entry in _logEntries)
            {
                if (stats.ContainsKey(entry.Level))
                {
                    stats[entry.Level]++;
                }
            }

            return stats;
        }

        /// <summary>
        /// افزودن ورودی لاگ با پارامترهای ساده
        /// </summary>
        private void AddLogEntry(LogLevel level, string message, string category, object context = null)
        {
            if (level < _config.MinimumLevel || !_config.EnableMemoryLogging)
                return;

            var entry = new LogEntry(level, message, category);
            entry.SetContext(context);
            
            AddLogEntry(entry);
        }

        /// <summary>
        /// افزودن ورودی لاگ به حافظه
        /// </summary>
        private void AddLogEntry(LogEntry entry)
        {
            if (!_config.EnableMemoryLogging)
                return;

            lock (_lockObject)
            {
                _logEntries.Enqueue(entry);
                
                // حذف لاگ‌های اضافی
                while (_logEntries.Count > _config.MaxMemoryEntries)
                {
                    _logEntries.TryDequeue(out _);
                }
            }

            // اطلاع‌رسانی به شنونده‌ها
            LogAdded?.Invoke(this, entry);
        }
    }
}