using System;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// کارخانه ایجاد لاگرها
    /// </summary>
    public static class LoggerFactory
    {
        private static ILogger _defaultLogger;
        private static readonly object _lock = new object();

        /// <summary>
        /// دریافت لاگر پیش‌فرض
        /// </summary>
        /// <returns>نمونه لاگر پیش‌فرض</returns>
        public static ILogger GetDefaultLogger()
        {
            if (_defaultLogger == null)
            {
                lock (_lock)
                {
                    if (_defaultLogger == null)
                    {
                        _defaultLogger = CreateDefaultLogger();
                    }
                }
            }

            return _defaultLogger;
        }

        /// <summary>
        /// تنظیم لاگر پیش‌فرض سفارشی
        /// </summary>
        /// <param name="logger">لاگر جدید</param>
        public static void SetDefaultLogger(ILogger logger)
        {
            lock (_lock)
            {
                // آزادسازی لاگر قبلی
                if (_defaultLogger is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _defaultLogger = logger;
            }
        }

        /// <summary>
        /// ایجاد لاگر فایلی
        /// </summary>
        /// <param name="config">تنظیمات لاگ‌گیری</param>
        /// <returns>نمونه FileLogger</returns>
        public static FileLogger CreateFileLogger(LoggingConfiguration config = null)
        {
            config ??= LoggingConfiguration.Default();
            return new FileLogger(config);
        }

        /// <summary>
        /// ایجاد لاگر حافظه‌ای
        /// </summary>
        /// <param name="config">تنظیمات لاگ‌گیری</param>
        /// <returns>نمونه MemoryLogger</returns>
        public static MemoryLogger CreateMemoryLogger(LoggingConfiguration config = null)
        {
            config ??= LoggingConfiguration.Default();
            return new MemoryLogger(config);
        }

        /// <summary>
        /// ایجاد لاگر ترکیبی
        /// </summary>
        /// <param name="config">تنظیمات لاگ‌گیری</param>
        /// <returns>نمونه CompositeLogger</returns>
        public static CompositeLogger CreateCompositeLogger(LoggingConfiguration config = null)
        {
            config ??= LoggingConfiguration.Default();

            var fileLogger = CreateFileLogger(config);
            var memoryLogger = CreateMemoryLogger(config);

            return new CompositeLogger(fileLogger, memoryLogger);
        }

        /// <summary>
        /// ایجاد لاگر پیش‌فرض
        /// </summary>
        /// <returns>نمونه لاگر پیش‌فرض</returns>
        private static ILogger CreateDefaultLogger()
        {
            try
            {
                var config = LoggingConfiguration.Default();
                return CreateCompositeLogger(config);
            }
            catch (Exception ex)
            {
                // در صورت خطا، لاگر ساده کنسولی برمی‌گردانیم
                Console.WriteLine($"خطا در ایجاد لاگر پیش‌فرض: {ex.Message}");
                return new ConsoleLogger();
            }
        }

        /// <summary>
        /// آزادسازی منابع
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                if (_defaultLogger is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _defaultLogger = null;
            }
        }
    }

    /// <summary>
    /// لاگر ساده کنسولی برای مواقع اضطراری
    /// </summary>
    internal class ConsoleLogger : ILogger
    {
        public void LogDebug(string message, object? context = null)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }

        public void LogInfo(string message, object? context = null)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        public void LogWarning(string message, object? context = null)
        {
            Console.WriteLine($"[WARNING] {message}");
        }

        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            Console.WriteLine($"[ERROR] {message}");
            if (exception != null)
            {
                Console.WriteLine($"Exception: {exception.Message}");
            }
        }

        public void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null)
        {
            Console.WriteLine($"[DOWNLOAD] {eventType}: {item?.FileName}");
        }

        public Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            return Task.FromResult(Enumerable.Empty<LogEntry>());
        }
    }
}