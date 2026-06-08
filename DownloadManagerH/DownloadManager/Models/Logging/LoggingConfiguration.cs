using System;
using System.IO;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// تنظیمات سیستم لاگ‌گیری
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// حداقل سطح لاگ برای ثبت
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// فعال بودن لاگ‌گیری در فایل
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// فعال بودن لاگ‌گیری در حافظه
        /// </summary>
        public bool EnableMemoryLogging { get; set; } = true;

        /// <summary>
        /// مسیر پوشه لاگ‌ها
        /// </summary>
        public string LogDirectory { get; set; } = Path.Combine(Settings.DataDirectory, "Logs");

        /// <summary>
        /// حداکثر اندازه فایل لاگ (بایت)
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10 مگابایت

        /// <summary>
        /// حداکثر تعداد فایل‌های لاگ نگهداری شده
        /// </summary>
        public int MaxFileCount { get; set; } = 10;

        /// <summary>
        /// حداکثر تعداد لاگ‌های نگهداری شده در حافظه
        /// </summary>
        public int MaxMemoryEntries { get; set; } = 1000;

        /// <summary>
        /// مدت زمان نگهداری لاگ‌ها (روز)
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// فرمت نام فایل لاگ
        /// </summary>
        public string FileNameFormat { get; set; } = "DownloadManager_{0:yyyy-MM-dd}.log";

        /// <summary>
        /// فعال بودن لاگ‌گیری رویدادهای دانلود
        /// </summary>
        public bool EnableDownloadEventLogging { get; set; } = true;

        /// <summary>
        /// فعال بودن لاگ‌گیری رویدادهای افزونه
        /// </summary>
        public bool EnablePluginEventLogging { get; set; } = true;

        /// <summary>
        /// فعال بودن لاگ‌گیری رویدادهای رابط کاربری
        /// </summary>
        public bool EnableUIEventLogging { get; set; } = false;

        /// <summary>
        /// بررسی صحت تنظیمات
        /// </summary>
        /// <returns>true اگر تنظیمات معتبر باشند</returns>
        public bool IsValid()
        {
            try
            {
                // بررسی مسیر لاگ
                if (string.IsNullOrWhiteSpace(LogDirectory))
                    return false;

                // بررسی اندازه فایل
                if (MaxFileSize <= 0)
                    return false;

                // بررسی تعداد فایل‌ها
                if (MaxFileCount <= 0)
                    return false;

                // بررسی تعداد ورودی‌های حافظه
                if (MaxMemoryEntries <= 0)
                    return false;

                // بررسی مدت نگهداری
                if (RetentionDays <= 0)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ایجاد پوشه لاگ در صورت عدم وجود
        /// </summary>
        public void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطا در ایجاد پوشه لاگ: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// دریافت مسیر فایل لاگ برای تاریخ مشخص
        /// </summary>
        /// <param name="date">تاریخ</param>
        /// <returns>مسیر فایل لاگ</returns>
        public string GetLogFilePath(DateTime date)
        {
            var fileName = string.Format(FileNameFormat, date);
            return Path.Combine(LogDirectory, fileName);
        }

        /// <summary>
        /// تنظیمات پیش‌فرض
        /// </summary>
        /// <returns>نمونه با تنظیمات پیش‌فرض</returns>
        public static LoggingConfiguration Default()
        {
            return new LoggingConfiguration();
        }
    }
}