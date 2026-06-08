using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// رابط اصلی سیستم لاگ‌گیری برای ثبت رویدادها و خطاها
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// ثبت پیام‌های اشکال‌زدایی
        /// </summary>
        /// <param name="message">متن پیام</param>
        /// <param name="context">اطلاعات زمینه‌ای اضافی</param>
        void LogDebug(string message, object? context = null);

        /// <summary>
        /// ثبت پیام‌های اطلاعاتی
        /// </summary>
        /// <param name="message">متن پیام</param>
        /// <param name="context">اطلاعات زمینه‌ای اضافی</param>
        void LogInfo(string message, object? context = null);

        /// <summary>
        /// ثبت پیام‌های هشدار
        /// </summary>
        /// <param name="message">متن پیام</param>
        /// <param name="context">اطلاعات زمینه‌ای اضافی</param>
        void LogWarning(string message, object? context = null);

        /// <summary>
        /// ثبت پیام‌های خطا
        /// </summary>
        /// <param name="message">متن پیام</param>
        /// <param name="exception">جزئیات استثنا</param>
        /// <param name="context">اطلاعات زمینه‌ای اضافی</param>
        void LogError(string message, Exception? exception = null, object? context = null);

        /// <summary>
        /// ثبت رویدادهای مربوط به دانلود
        /// </summary>
        /// <param name="item">آیتم دانلود</param>
        /// <param name="eventType">نوع رویداد</param>
        /// <param name="additionalData">داده‌های اضافی</param>
        void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null);

        /// <summary>
        /// دریافت لاگ‌های ثبت شده با فیلتر
        /// </summary>
        /// <param name="minLevel">حداقل سطح لاگ</param>
        /// <param name="from">تاریخ شروع</param>
        /// <param name="to">تاریخ پایان</param>
        /// <returns>لیست لاگ‌های فیلتر شده</returns>
        Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null);
    }
}