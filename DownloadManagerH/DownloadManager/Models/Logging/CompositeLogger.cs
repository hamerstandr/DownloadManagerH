using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// لاگر ترکیبی که چندین لاگر را با هم ترکیب می‌کند
    /// </summary>
    public class CompositeLogger : ILogger, IDisposable
    {
        private readonly List<ILogger> _loggers;
        private bool _disposed = false;

        /// <summary>
        /// سازنده CompositeLogger
        /// </summary>
        /// <param name="loggers">لیست لاگرهای ترکیبی</param>
        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = new List<ILogger>(loggers ?? throw new ArgumentNullException(nameof(loggers)));
        }

        /// <summary>
        /// سازنده CompositeLogger با لیست
        /// </summary>
        /// <param name="loggers">لیست لاگرهای ترکیبی</param>
        public CompositeLogger(IEnumerable<ILogger> loggers)
        {
            _loggers = new List<ILogger>(loggers ?? throw new ArgumentNullException(nameof(loggers)));
        }

        /// <summary>
        /// افزودن لاگر جدید
        /// </summary>
        /// <param name="logger">لاگر جدید</param>
        public void AddLogger(ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (!_loggers.Contains(logger))
            {
                _loggers.Add(logger);
            }
        }

        /// <summary>
        /// حذف لاگر
        /// </summary>
        /// <param name="logger">لاگر مورد نظر برای حذف</param>
        /// <returns>true اگر حذف شد</returns>
        public bool RemoveLogger(ILogger logger)
        {
            return _loggers.Remove(logger);
        }

        /// <summary>
        /// دریافت تعداد لاگرهای فعال
        /// </summary>
        public int LoggerCount => _loggers.Count;

        /// <summary>
        /// ثبت پیام اشکال‌زدایی در همه لاگرها
        /// </summary>
        public void LogDebug(string message, object? context = null)
        {
            ExecuteOnAllLoggers(logger => logger.LogDebug(message, context));
        }

        /// <summary>
        /// ثبت پیام اطلاعاتی در همه لاگرها
        /// </summary>
        public void LogInfo(string message, object? context = null)
        {
            ExecuteOnAllLoggers(logger => logger.LogInfo(message, context));
        }

        /// <summary>
        /// ثبت پیام هشدار در همه لاگرها
        /// </summary>
        public void LogWarning(string message, object? context = null)
        {
            ExecuteOnAllLoggers(logger => logger.LogWarning(message, context));
        }

        /// <summary>
        /// ثبت پیام خطا در همه لاگرها
        /// </summary>
        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            ExecuteOnAllLoggers(logger => logger.LogError(message, exception, context));
        }

        /// <summary>
        /// ثبت رویداد دانلود در همه لاگرها
        /// </summary>
        public void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null)
        {
            ExecuteOnAllLoggers(logger => logger.LogDownloadEvent(item, eventType, additionalData));
        }

        /// <summary>
        /// دریافت لاگ‌ها از اولین لاگری که قابلیت خواندن دارد
        /// </summary>
        public async Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            // سعی می‌کنیم از اولین لاگر که داده برمی‌گرداند استفاده کنیم
            foreach (var logger in _loggers)
            {
                try
                {
                    var logs = await logger.GetLogsAsync(minLevel, from, to);
                    if (logs != null && logs.Any())
                    {
                        return logs;
                    }
                }
                catch (Exception ex)
                {
                    // در صورت خطا، لاگ می‌کنیم و به لاگر بعدی می‌رویم
                    Console.WriteLine($"خطا در دریافت لاگ از {logger.GetType().Name}: {ex.Message}");
                }
            }

            // اگر هیچ لاگری داده نداشت، لیست خالی برمی‌گردانیم
            return Enumerable.Empty<LogEntry>();
        }

        /// <summary>
        /// دریافت لاگ‌ها از همه لاگرها و ترکیب آنها
        /// </summary>
        /// <param name="minLevel">حداقل سطح لاگ</param>
        /// <param name="from">تاریخ شروع</param>
        /// <param name="to">تاریخ پایان</param>
        /// <returns>لاگ‌های ترکیب شده از همه منابع</returns>
        public async Task<IEnumerable<LogEntry>> GetCombinedLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            var allLogs = new List<LogEntry>();

            foreach (var logger in _loggers)
            {
                try
                {
                    var logs = await logger.GetLogsAsync(minLevel, from, to);
                    if (logs != null)
                    {
                        allLogs.AddRange(logs);
                    }
                }
                catch (Exception ex)
                {
                    // در صورت خطا، لاگ می‌کنیم و ادامه می‌دهیم
                    Console.WriteLine($"خطا در دریافت لاگ از {logger.GetType().Name}: {ex.Message}");
                }
            }

            // مرتب‌سازی بر اساس زمان و حذف تکراری‌ها
            return allLogs
                .GroupBy(l => new { l.Timestamp, l.Level, l.Message, l.Category })
                .Select(g => g.First())
                .OrderBy(l => l.Timestamp);
        }

        /// <summary>
        /// اجرای عملیات روی همه لاگرها با مدیریت خطا
        /// </summary>
        /// <param name="action">عملیات مورد نظر</param>
        private void ExecuteOnAllLoggers(Action<ILogger> action)
        {
            if (_disposed)
                return;

            var exceptions = new List<Exception>();

            foreach (var logger in _loggers.ToList()) // کپی برای جلوگیری از تداخل
            {
                try
                {
                    action(logger);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    // در صورت خطا، لاگ می‌کنیم در کنسول
                    Console.WriteLine($"خطا در لاگر {logger.GetType().Name}: {ex.Message}");
                }
            }

            // اگر همه لاگرها خطا داشتند، استثنا پرتاب می‌کنیم
            if (exceptions.Count == _loggers.Count && exceptions.Count > 0)
            {
                throw new AggregateException("همه لاگرها با خطا مواجه شدند", exceptions);
            }
        }

        /// <summary>
        /// دریافت اطلاعات وضعیت لاگرها
        /// </summary>
        /// <returns>اطلاعات وضعیت</returns>
        public Dictionary<string, object> GetStatus()
        {
            var status = new Dictionary<string, object>
            {
                { "TotalLoggers", _loggers.Count },
                { "LoggerTypes", _loggers.Select(l => l.GetType().Name).ToList() }
            };

            // اطلاعات اضافی از لاگرهای خاص
            foreach (var logger in _loggers)
            {
                try
                {
                    if (logger is MemoryLogger memoryLogger)
                    {
                        status[$"MemoryLogger_Count"] = memoryLogger.Count;
                        status[$"MemoryLogger_Stats"] = memoryLogger.GetLogStatistics();
                    }
                }
                catch
                {
                    // نادیده گرفتن خطاهای دریافت وضعیت
                }
            }

            return status;
        }

        /// <summary>
        /// آزادسازی منابع
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var logger in _loggers)
                {
                    if (logger is IDisposable disposableLogger)
                    {
                        try
                        {
                            disposableLogger.Dispose();
                        }
                        catch
                        {
                            // نادیده گرفتن خطاهای dispose
                        }
                    }
                }

                _loggers.Clear();
                _disposed = true;
            }
        }
    }
}