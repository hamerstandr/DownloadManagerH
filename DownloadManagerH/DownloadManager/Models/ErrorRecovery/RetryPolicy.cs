using System;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models.ErrorRecovery
{
    /// <summary>
    /// سیاست تلاش مجدد برای عملیات‌های ناموفق
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// حداکثر تعداد تلاش مجدد
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// تأخیر اولیه بین تلاش‌ها
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// ضریب افزایش تأخیر (Exponential Backoff)
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// حداکثر تأخیر بین تلاش‌ها
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// فعال بودن Jitter برای جلوگیری از همزمانی تلاش‌ها
        /// </summary>
        public bool EnableJitter { get; set; } = true;

        private readonly Random _random = new Random();
        private readonly ILogger _logger;

        /// <summary>
        /// سازنده RetryPolicy
        /// </summary>
        /// <param name="logger">لاگر برای ثبت تلاش‌ها</param>
        public RetryPolicy(ILogger logger = null)
        {
            _logger = logger ?? LoggerFactory.GetDefaultLogger();
        }

        /// <summary>
        /// اجرای عملیات با تلاش مجدد
        /// </summary>
        /// <typeparam name="T">نوع نتیجه</typeparam>
        /// <param name="operation">عملیات مورد نظر</param>
        /// <param name="operationName">نام عملیات برای لاگ</param>
        /// <returns>نتیجه عملیات</returns>
        public async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, string operationName = "عملیات")
        {
            Exception lastException = null;
            
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        var delay = CalculateDelay(attempt);
                        _logger.LogInfo($"تلاش مجدد {attempt} از {MaxRetries} برای {operationName} - تأخیر: {delay.TotalSeconds} ثانیه");
                        await Task.Delay(delay);
                    }

                    _logger.LogDebug($"شروع تلاش {attempt + 1} برای {operationName}");
                    var result = await operation();
                    
                    if (attempt > 0)
                    {
                        _logger.LogInfo($"{operationName} پس از {attempt} تلاش مجدد موفق شد");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt == MaxRetries)
                    {
                        _logger.LogError($"{operationName} پس از {MaxRetries} تلاش مجدد ناموفق ماند", ex);
                        break;
                    }
                    
                    if (!ShouldRetry(ex))
                    {
                        _logger.LogWarning($"{operationName} به دلیل نوع خطا قابل تلاش مجدد نیست: {ex.GetType().Name}");
                        break;
                    }
                    
                    _logger.LogWarning($"تلاش {attempt + 1} برای {operationName} ناموفق - خطا: {ex.Message}");
                }
            }

            throw new RetryExhaustedException($"تمام تلاش‌های مجدد برای {operationName} ناموفق بود", lastException);
        }

        /// <summary>
        /// اجرای عملیات بدون نتیجه با تلاش مجدد
        /// </summary>
        /// <param name="operation">عملیات مورد نظر</param>
        /// <param name="operationName">نام عملیات برای لاگ</param>
        public async Task ExecuteWithRetry(Func<Task> operation, string operationName = "عملیات")
        {
            await ExecuteWithRetry(async () =>
            {
                await operation();
                return true; // مقدار dummy برای سازگاری با generic method
            }, operationName);
        }

        /// <summary>
        /// محاسبه تأخیر برای تلاش بعدی
        /// </summary>
        /// <param name="attempt">شماره تلاش</param>
        /// <returns>مدت تأخیر</returns>
        private TimeSpan CalculateDelay(int attempt)
        {
            // محاسبه تأخیر با Exponential Backoff
            var delay = TimeSpan.FromMilliseconds(
                InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1)
            );

            // اعمال حداکثر تأخیر
            if (delay > MaxDelay)
            {
                delay = MaxDelay;
            }

            // اعمال Jitter برای جلوگیری از همزمانی
            if (EnableJitter)
            {
                var jitterRange = delay.TotalMilliseconds * 0.1; // 10% jitter
                var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange;
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
            }

            return delay;
        }

        /// <summary>
        /// تعیین اینکه آیا خطا قابل تلاش مجدد است یا نه
        /// </summary>
        /// <param name="exception">استثنای رخ داده</param>
        /// <returns>true اگر قابل تلاش مجدد باشد</returns>
        private bool ShouldRetry(Exception exception)
        {
            // خطاهای قابل تلاش مجدد
            return exception switch
            {
                System.Net.Http.HttpRequestException => true,
                TaskCanceledException => true,
                System.Net.Sockets.SocketException => true,
                System.IO.IOException => true,
                TimeoutException => true,
                
                // خطاهای غیرقابل تلاش مجدد - ترتیب مهم است
                ArgumentNullException => false,
                ArgumentException => false,
                UnauthorizedAccessException => false,
                System.Security.SecurityException => false,
                
                // سایر خطاها قابل تلاش مجدد هستند
                _ => true
            };
        }

        /// <summary>
        /// ایجاد سیاست پیش‌فرض برای دانلود
        /// </summary>
        /// <returns>سیاست پیش‌فرض</returns>
        public static RetryPolicy ForDownload(ILogger logger = null)
        {
            return new RetryPolicy(logger)
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.FromSeconds(2),
                BackoffMultiplier = 2.0,
                MaxDelay = TimeSpan.FromMinutes(2),
                EnableJitter = true
            };
        }

        /// <summary>
        /// ایجاد سیاست برای عملیات‌های شبکه
        /// </summary>
        /// <returns>سیاست شبکه</returns>
        public static RetryPolicy ForNetwork(ILogger logger = null)
        {
            return new RetryPolicy(logger)
            {
                MaxRetries = 5,
                InitialDelay = TimeSpan.FromSeconds(1),
                BackoffMultiplier = 1.5,
                MaxDelay = TimeSpan.FromMinutes(1),
                EnableJitter = true
            };
        }

        /// <summary>
        /// ایجاد سیاست برای عملیات‌های فایل
        /// </summary>
        /// <returns>سیاست فایل</returns>
        public static RetryPolicy ForFileOperations(ILogger logger = null)
        {
            return new RetryPolicy(logger)
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromMilliseconds(500),
                BackoffMultiplier = 2.0,
                MaxDelay = TimeSpan.FromSeconds(10),
                EnableJitter = false
            };
        }
    }

    /// <summary>
    /// استثنای تمام شدن تلاش‌های مجدد
    /// </summary>
    public class RetryExhaustedException : Exception
    {
        /// <summary>
        /// آخرین استثنای رخ داده
        /// </summary>
        public Exception LastException { get; }

        /// <summary>
        /// سازنده RetryExhaustedException
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <param name="lastException">آخرین استثنا</param>
        public RetryExhaustedException(string message, Exception lastException) 
            : base(message, lastException)
        {
            LastException = lastException;
        }
    }
}