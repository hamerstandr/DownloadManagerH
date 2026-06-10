using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// حالت فیلتر (لیست سفید یا سیاه)
    /// </summary>
    public enum FilterMode
    {
        WhiteList, // فقط موارد مجاز
        BlackList  // همه بجز موارد مسدود
    }

    /// <summary>
    /// تنظیمات پیشرفته برای مدیریت دانلود
    /// </summary>
    public static class Settings
    {
        private static readonly object _lock = new object();
        private static string _settingsFilePath => Path.Combine(DataDirectory, "settings.json");

        // ==================== تنظیمات اصلی ====================
        public static int ParallelParts { get; set; } = 4;
        public static int MaxConcurrentDownloadsLimit { get; set; } = 5;
        public static bool EnableStartup { get; set; } = false;
        public static bool MonitorClipboard { get; set; } = true;
        public static bool AddDownloadsDirectly { get; set; } = false;
        public static bool EnableTrafficWatchIntegration { get; set; } = true;
        public static int TrafficWatchPort { get; set; } = 9090;
        public static string Language { get; set; } = "fa";
        public static string ThemeColor { get; set; } = "#4caf50";
        public static int CountConctionDownloads { get; set; } = 3;
        
        public static string DefaultDownloadPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        // ==================== تنظیمات پیشرفته فیلتر ====================
        /// <summary>
        /// حالت فیلتر برای پسوند فایل‌ها
        /// </summary>
        public static FilterMode ExtensionFilterMode { get; set; } = FilterMode.WhiteList;
        
        /// <summary>
        /// لیست پسوندهای قابل دانلود (بر اساس حالت فیلتر عمل می‌کند)
        /// </summary>
        public static HashSet<string> DownloadableExtensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // فایل‌های فشرده
            "zip", "rar", "7z", "tar", "gz", "bz2", "xz",
            
            // فایل‌های اجرایی
            "exe", "msi", "dmg", "pkg", "deb", "rpm", "appimage",
            
            // فایل‌های سند
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp",
            
            // فایل‌های رسانه‌ای - ویدیو
            "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm", "m4v", "mpeg", "mpg", "3gp",
            
            // فایل‌های رسانه‌ای - صدا
            "mp3", "wav", "flac", "aac", "ogg", "wma", "m4a",
            
            // فایل‌های رسانه‌ای - تصویر
            "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "ico", "tiff", "tif",
            
            // فایل‌های دیسک
            "iso", "img", "bin", "cue",
            
            // فایل‌های موبایل
            "apk", "ipa",
            
            // سایر
            "torrent", "magnet"
        };

        /// <summary>
        /// لیست سیاه دامنه‌ها (دامنه‌هایی که نباید دانلود از آن‌ها رهگیری شود)
        /// </summary>
        public static HashSet<string> DomainBlacklist { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "127.0.0.1",
            "::1"
        };

        /// <summary>
        /// لیست سفید دامنه‌ها (فقط این دامنه‌ها رهگیری شوند - اگر خالی باشد، همه دامنه‌ها مجازند)
        /// </summary>
        public static HashSet<string> DomainWhitelist { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// حالت فیلتر دامنه
        /// </summary>
        public static FilterMode DomainFilterMode { get; set; } = FilterMode.BlackList;

        /// <summary>
        /// الگوهای URL برای فیلتر کردن (Regex)
        /// </summary>
        public static List<string> UrlPatterns { get; set; } = new List<string>();

        /// <summary>
        /// حداقل اندازه فایل برای رهگیری (بایت)
        /// </summary>
        public static long MinFileSizeForInterception { get; set; } = 100 * 1024; // 100KB

        /// <summary>
        /// حداکثر اندازه فایل برای رهگیری (0 یعنی بدون محدودیت)
        /// </summary>
        public static long MaxFileSizeForInterception { get; set; } = 0;

        // ==================== مسیرها ====================
        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            App.appName
        );

        public static string TempDirectory => Path.Combine(DataDirectory, "temp");
        
        // ==================== متدها ====================
        
        /// <summary>
        /// دریافت فرهنگ بر اساس زبان انتخاب شده
        /// </summary>
        public static CultureInfo GetCultureInfo()
        {
            return Language.ToLower() switch
            {
                "ar" => new CultureInfo("ar-SA"),
                "en" => new CultureInfo("en-US"),
                _ => new CultureInfo("fa-IR")
            };
        }
        
        /// <summary>
        /// اعمال جهت متن بر اساس زبان
        /// </summary>
        public static FlowDirection GetFlowDirection()
        {
            return Language.ToLower() == "en" 
                ? FlowDirection.LeftToRight 
                : FlowDirection.RightToLeft;
        }

        /// <summary>
        /// بررسی اینکه آیا پسوند فایل مجاز است
        /// </summary>
        public static bool IsExtensionAllowed(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return ExtensionFilterMode == FilterMode.BlackList;

            var ext = extension.TrimStart('.').ToLower();
            bool exists = DownloadableExtensions.Contains(ext);

            return ExtensionFilterMode switch
            {
                FilterMode.WhiteList => exists,
                FilterMode.BlackList => !exists,
                _ => true
            };
        }

        /// <summary>
        /// بررسی اینکه آیا دامنه مجاز است
        /// </summary>
        public static bool IsDomainAllowed(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host;

                if (DomainFilterMode == FilterMode.WhiteList)
                {
                    return DomainWhitelist.Count == 0 || DomainWhitelist.Contains(host);
                }
                else // BlackList
                {
                    return !DomainBlacklist.Contains(host);
                }
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// افزودن پسوند به لیست
        /// </summary>
        public static void AddExtension(string extension)
        {
            lock (_lock)
            {
                DownloadableExtensions.Add(extension.TrimStart('.').ToLower());
            }
        }

        /// <summary>
        /// حذف پسوند از لیست
        /// </summary>
        public static void RemoveExtension(string extension)
        {
            lock (_lock)
            {
                DownloadableExtensions.Remove(extension.TrimStart('.').ToLower());
            }
        }

        /// <summary>
        /// ذخیره تنظیمات در فایل JSON
        /// </summary>
        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(DataDirectory);
                    
                    var settingsData = new SettingsData
                    {
                        ParallelParts = ParallelParts,
                        MaxConcurrentDownloadsLimit = MaxConcurrentDownloadsLimit,
                        EnableStartup = EnableStartup,
                        MonitorClipboard = MonitorClipboard,
                        AddDownloadsDirectly = AddDownloadsDirectly,
                        EnableTrafficWatchIntegration = EnableTrafficWatchIntegration,
                        TrafficWatchPort = TrafficWatchPort,
                        Language = Language,
                        ThemeColor = ThemeColor,
                        CountConctionDownloads = CountConctionDownloads,
                        DefaultDownloadPath = DefaultDownloadPath,
                        ExtensionFilterMode = ExtensionFilterMode,
                        DownloadableExtensions = DownloadableExtensions.ToList(),
                        DomainBlacklist = DomainBlacklist.ToList(),
                        DomainWhitelist = DomainWhitelist.ToList(),
                        DomainFilterMode = DomainFilterMode,
                        UrlPatterns = UrlPatterns,
                        MinFileSizeForInterception = MinFileSizeForInterception,
                        MaxFileSizeForInterception = MaxFileSizeForInterception
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    };

                    string json = JsonSerializer.Serialize(settingsData, options);
                    File.WriteAllText(_settingsFilePath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطا در ذخیره تنظیمات: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// بارگذاری تنظیمات از فایل JSON
        /// </summary>
        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_settingsFilePath))
                        return;

                    string json = File.ReadAllText(_settingsFilePath);
                    var settingsData = JsonSerializer.Deserialize<SettingsData>(json);

                    if (settingsData != null)
                    {
                        ParallelParts = settingsData.ParallelParts;
                        MaxConcurrentDownloadsLimit = settingsData.MaxConcurrentDownloadsLimit;
                        EnableStartup = settingsData.EnableStartup;
                        MonitorClipboard = settingsData.MonitorClipboard;
                        AddDownloadsDirectly = settingsData.AddDownloadsDirectly;
                        EnableTrafficWatchIntegration = settingsData.EnableTrafficWatchIntegration;
                        TrafficWatchPort = settingsData.TrafficWatchPort;
                        Language = settingsData.Language;
                        ThemeColor = settingsData.ThemeColor;
                        CountConctionDownloads = settingsData.CountConctionDownloads;
                        DefaultDownloadPath = settingsData.DefaultDownloadPath;
                        ExtensionFilterMode = settingsData.ExtensionFilterMode;
                        DownloadableExtensions = new HashSet<string>(settingsData.DownloadableExtensions, StringComparer.OrdinalIgnoreCase);
                        DomainBlacklist = new HashSet<string>(settingsData.DomainBlacklist, StringComparer.OrdinalIgnoreCase);
                        DomainWhitelist = new HashSet<string>(settingsData.DomainWhitelist, StringComparer.OrdinalIgnoreCase);
                        DomainFilterMode = settingsData.DomainFilterMode;
                        UrlPatterns = settingsData.UrlPatterns ?? new List<string>();
                        MinFileSizeForInterception = settingsData.MinFileSizeForInterception;
                        MaxFileSizeForInterception = settingsData.MaxFileSizeForInterception;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطا در بارگذاری تنظیمات: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// بازنشانی تنظیمات به مقادیر پیش‌فرض
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                ParallelParts = 4;
                MaxConcurrentDownloadsLimit = 5;
                EnableStartup = false;
                MonitorClipboard = true;
                AddDownloadsDirectly = false;
                EnableTrafficWatchIntegration = true;
                TrafficWatchPort = 9090;
                Language = "fa";
                ThemeColor = "#4caf50";
                CountConctionDownloads = 3;
                DefaultDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );
                ExtensionFilterMode = FilterMode.WhiteList;
                DomainFilterMode = FilterMode.BlackList;
                UrlPatterns = new List<string>();
                MinFileSizeForInterception = 100 * 1024;
                MaxFileSizeForInterception = 0;
                
                DownloadableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "zip", "rar", "7z", "tar", "gz", "bz2", "xz",
                    "exe", "msi", "dmg", "pkg", "deb", "rpm", "appimage",
                    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
                    "mp3", "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm",
                    "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp",
                    "iso", "img", "bin", "apk", "ipa"
                };
                
                DomainBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "localhost", "127.0.0.1", "::1"
                };
                
                DomainWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// کلاس برای سریالایز کردن تنظیمات
    /// </summary>
    public class SettingsData
    {
        public int ParallelParts { get; set; } = 4;
        public int MaxConcurrentDownloadsLimit { get; set; } = 5;
        public bool EnableStartup { get; set; } = false;
        public bool MonitorClipboard { get; set; } = true;
        public bool AddDownloadsDirectly { get; set; } = false;
        public bool EnableTrafficWatchIntegration { get; set; } = true;
        public int TrafficWatchPort { get; set; } = 9090;
        public string Language { get; set; } = "fa";
        public string ThemeColor { get; set; } = "#4caf50";
        public int CountConctionDownloads { get; set; } = 3;
        public string DefaultDownloadPath { get; set; } = "";
        public FilterMode ExtensionFilterMode { get; set; } = FilterMode.WhiteList;
        public List<string> DownloadableExtensions { get; set; } = new List<string>();
        public List<string> DomainBlacklist { get; set; } = new List<string>();
        public List<string> DomainWhitelist { get; set; } = new List<string>();
        public FilterMode DomainFilterMode { get; set; } = FilterMode.BlackList;
        public List<string> UrlPatterns { get; set; } = new List<string>();
        public long MinFileSizeForInterception { get; set; } = 102400;
        public long MaxFileSizeForInterception { get; set; } = 0;
    }
} 