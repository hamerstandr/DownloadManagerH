using System;
using System.Collections.Generic;
using System.IO;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// تنظیمات رهگیری و اولویت‌بندی دانلودها
    /// </summary>
    public class DownloadInterceptionSettings
    {
        /// <summary>
        /// فعال/غیرفعال بودن رهگیری دانلود
        /// </summary>
        public bool EnableDownloadInterception { get; set; } = true;

        /// <summary>
        /// حداقل اندازه فایل برای رهگیری (بایت)
        /// </summary>
        public long MinFileSizeForInterception { get; set; } = 100 * 1024; // 100KB

        /// <summary>
        /// حداکثر اندازه فایل برای رهگیری (بایت) - 0 یعنی بدون محدودیت
        /// </summary>
        public long MaxFileSizeForInterception { get; set; } = 0;

        /// <summary>
        /// رهگیری خودکار تمام دانلودها
        /// </summary>
        public bool AutoInterceptAllDownloads { get; set; } = false;

        /// <summary>
        /// رهگیری فقط دانلودهای بزرگ
        /// </summary>
        public bool InterceptLargeFilesOnly { get; set; } = true;

        /// <summary>
        /// حد آستانه برای دانلودهای بزرگ (مگابایت)
        /// </summary>
        public int LargeFileThresholdMB { get; set; } = 10;

        /// <summary>
        /// انواع فایل‌های قابل رهگیری
        /// </summary>
        public HashSet<string> InterceptableFileTypes { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // فایل‌های فشرده
            "zip", "rar", "7z", "tar", "gz", "bz2", "xz",
            
            // فایل‌های اجرایی
            "exe", "msi", "dmg", "pkg", "deb", "rpm", "appimage",
            
            // فایل‌های سند
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
            
            // فایل‌های رسانه‌ای
            "mp3", "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm",
            "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp",
            
            // فایل‌های دیسک
            "iso", "img", "bin",
            
            // فایل‌های موبایل
            "apk", "ipa"
        };

        /// <summary>
        /// انواع MIME type قابل رهگیری
        /// </summary>
        public HashSet<string> InterceptableMimeTypes { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/zip",
            "application/x-rar-compressed",
            "application/x-7z-compressed",
            "application/octet-stream",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument",
            "video/",
            "audio/",
            "image/"
        };

        /// <summary>
        /// رهگیری فایل‌های با نوع نامشخص
        /// </summary>
        public bool InterceptUnknownFileTypes { get; set; } = false;

        /// <summary>
        /// مسیر پیش‌فرض ذخیره فایل‌های رهگیری شده
        /// </summary>
        public string? DefaultSavePath { get; set; }

        /// <summary>
        /// فهرست دامنه‌های مستثنی از رهگیری
        /// </summary>
        public HashSet<string> ExcludedDomains { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "127.0.0.1",
            "::1"
        };

        /// <summary>
        /// فهرست الگوهای URL مستثنی از رهگیری (Regex patterns)
        /// </summary>
        public HashSet<string> ExcludedUrlPatterns { get; set; } = new HashSet<string>();

        /// <summary>
        /// تأخیر قبل از رهگیری دانلود (میلی‌ثانیه)
        /// </summary>
        public int InterceptionDelayMs { get; set; } = 500;

        /// <summary>
        /// نمایش اعلان هنگام رهگیری دانلود
        /// </summary>
        public bool ShowInterceptionNotification { get; set; } = true;

        /// <summary>
        /// اولویت‌بندی بر اساس مرورگر
        /// </summary>
        public Dictionary<string, int> BrowserPriorityOverrides { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// فعال‌سازی اولویت‌بندی پیشرفته
        /// </summary>
        public bool EnableAdvancedPriority { get; set; } = true;

        /// <summary>
        /// اولویت Edge در مقایسه با سایر مرورگرها
        /// </summary>
        public bool EdgeHasHighestPriority { get; set; } = true;

        /// <summary>
        /// تأخیر پردازش پیام‌های اولویت پایین (میلی‌ثانیه)
        /// </summary>
        public int LowPriorityDelayMs { get; set; } = 100;

        /// <summary>
        /// حداکثر تعداد پیام‌های همزمان در صف
        /// </summary>
        public int MaxConcurrentMessages { get; set; } = 10;

        /// <summary>
        /// فعال‌سازی صف اولویت‌دار برای پیام‌ها
        /// </summary>
        public bool EnablePriorityQueue { get; set; } = true;

        /// <summary>
        /// آستانه امتیاز برای رهگیری دانلود (0.0 - 1.0)
        /// </summary>
        public double InterceptionThreshold { get; set; } = 0.5;

        /// <summary>
        /// دریافت مسیر پیش‌فرض دانلود
        /// </summary>
        public string GetDefaultDownloadPath()
        {
            if (!string.IsNullOrEmpty(DefaultSavePath) && Directory.Exists(DefaultSavePath))
            {
                return DefaultSavePath;
            }

            // استفاده از مسیر Downloads کاربر
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloadsPath))
            {
                return downloadsPath;
            }

            // در نهایت از Desktop استفاده کن
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        /// <summary>
        /// بررسی اینکه آیا دامنه مستثنی است
        /// </summary>
        public bool IsDomainExcluded(string url)
        {
            try
            {
                var uri = new Uri(url);
                return ExcludedDomains.Contains(uri.Host);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// بررسی اینکه آیا URL با الگوهای مستثنی مطابقت دارد
        /// </summary>
        public bool IsUrlPatternExcluded(string url)
        {
            foreach (var pattern in ExcludedUrlPatterns)
            {
                try
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // اگر الگو معتبر نباشد، نادیده بگیر
                }
            }
            return false;
        }
    }

    /// <summary>
    /// آمار رهگیری دانلودها
    /// </summary>
    public class DownloadInterceptionStats
    {
        /// <summary>
        /// تعداد کل دانلودهای پردازش شده
        /// </summary>
        public int TotalProcessedDownloads { get; set; } = 0;

        /// <summary>
        /// تعداد کل دانلودهای رهگیری شده
        /// </summary>
        public int TotalInterceptedDownloads { get; set; } = 0;

        /// <summary>
        /// تعداد دانلودها بر اساس مرورگر
        /// </summary>
        public Dictionary<string, int> DownloadsByBrowser { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// تعداد دانلودهای رهگیری شده بر اساس مرورگر
        /// </summary>
        public Dictionary<string, int> InterceptedByBrowser { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// زمان آخرین ریست آمار
        /// </summary>
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// درصد موفقیت رهگیری
        /// </summary>
        public double InterceptionSuccessRate => TotalProcessedDownloads > 0 
            ? (double)TotalInterceptedDownloads / TotalProcessedDownloads * 100 
            : 0;

        /// <summary>
        /// دریافت آمار مرورگر خاص
        /// </summary>
        public BrowserStats GetBrowserStats(string browser)
        {
            return new BrowserStats
            {
                Browser = browser,
                TotalDownloads = DownloadsByBrowser.GetValueOrDefault(browser, 0),
                InterceptedDownloads = InterceptedByBrowser.GetValueOrDefault(browser, 0)
            };
        }

        /// <summary>
        /// دریافت فهرست مرورگرهای فعال
        /// </summary>
        public List<string> GetActiveBrowsers()
        {
            var browsers = new HashSet<string>();
            browsers.UnionWith(DownloadsByBrowser.Keys);
            browsers.UnionWith(InterceptedByBrowser.Keys);
            return browsers.ToList();
        }
    }

    /// <summary>
    /// آمار مرورگر خاص
    /// </summary>
    public class BrowserStats
    {
        public string Browser { get; set; } = "";
        public int TotalDownloads { get; set; } = 0;
        public int InterceptedDownloads { get; set; } = 0;
        public double InterceptionRate => TotalDownloads > 0 
            ? (double)InterceptedDownloads / TotalDownloads * 100 
            : 0;
    }

    /// <summary>
    /// نتیجه پردازش دانلود
    /// </summary>
    public class DownloadProcessingResult
    {
        /// <summary>
        /// موفقیت عملیات
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// پیام نتیجه
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// تعداد دانلودهای پردازش شده
        /// </summary>
        public int ProcessedCount { get; set; } = 0;

        /// <summary>
        /// تعداد خطاها
        /// </summary>
        public int ErrorCount { get; set; } = 0;

        /// <summary>
        /// فهرست خطاها
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// فهرست URLهای پردازش شده
        /// </summary>
        public List<string> ProcessedUrls { get; set; } = new List<string>();

        /// <summary>
        /// زمان پردازش
        /// </summary>
        public DateTime ProcessingTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event Args برای رهگیری دانلود
    /// </summary>
    public class DownloadInterceptedEventArgs : EventArgs
    {
        public DownloadItem DownloadItem { get; set; } = null!;
        public string Browser { get; set; } = "";
        public NativeMessagingProtocol.BrowserPriority Priority { get; set; }
        public string OriginalDownloadId { get; set; } = "";
        public DateTime InterceptionTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event Args برای پردازش دانلود
    /// </summary>
    public class DownloadProcessedEventArgs : EventArgs
    {
        public DownloadItem DownloadItem { get; set; } = null!;
        public string Browser { get; set; } = "";
        public NativeMessagingProtocol.BrowserPriority Priority { get; set; }
        public DateTime ProcessingTime { get; set; } = DateTime.UtcNow;
    }


}