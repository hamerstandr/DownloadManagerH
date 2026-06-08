using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت رابط کاربری برای تنظیمات رهگیری دانلود
    /// </summary>
    public class DownloadInterceptionUIManager
    {
        private readonly ILogger _logger;
        private readonly NativeMessagingSettingsManager _settingsManager;
        private readonly DownloadInterceptionManager _interceptionManager;

        // رویدادها برای اطلاع‌رسانی تغییرات UI
        public event EventHandler<InterceptionSettingsChangedEventArgs>? SettingsChanged;
        public event EventHandler<InterceptionStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<InterceptionNotificationEventArgs>? NotificationRequested;

        public DownloadInterceptionUIManager(
            ILogger logger, 
            NativeMessagingSettingsManager settingsManager,
            DownloadInterceptionManager interceptionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _interceptionManager = interceptionManager ?? throw new ArgumentNullException(nameof(interceptionManager));

            // اشتراک در رویدادهای تغییر تنظیمات
            _settingsManager.SettingsChanged += OnSettingsChanged;
            _interceptionManager.DownloadIntercepted += OnDownloadIntercepted;
            _interceptionManager.DownloadProcessed += OnDownloadProcessed;

            _logger.LogInfo("Download Interception UI Manager initialized");
        }

        /// <summary>
        /// دریافت تنظیمات فعلی برای نمایش در UI
        /// </summary>
        public DownloadInterceptionUISettings GetUISettings()
        {
            var settings = _settingsManager.Settings;
            var stats = _interceptionManager.GetStats();

            return new DownloadInterceptionUISettings
            {
                // تنظیمات اصلی
                EnableDownloadInterception = settings.EnableDownloadInterception,
                AutoInterceptAllDownloads = settings.AutoInterceptAllDownloads,
                InterceptLargeFilesOnly = settings.InterceptLargeFilesOnly,
                LargeFileThresholdMB = settings.LargeFileThresholdMB,
                MinFileSizeForInterceptionMB = settings.MinFileSizeForInterception / (1024.0 * 1024.0),
                MaxFileSizeForInterceptionMB = settings.MaxFileSizeForInterception > 0 
                    ? settings.MaxFileSizeForInterception / (1024.0 * 1024.0) 
                    : 0,

                // تنظیمات پیشرفته
                EnableAdvancedPriority = settings.EnableAdvancedPriority,
                EdgeHasHighestPriority = settings.EdgeHasHighestPriority,
                EnablePriorityQueue = settings.EnablePriorityQueue,
                InterceptionThreshold = settings.InterceptionThreshold,

                // تنظیمات اعلان‌ها
                ShowInterceptionNotification = settings.ShowInterceptionNotification,
                InterceptionDelayMs = settings.InterceptionDelayMs,

                // مسیر ذخیره
                DefaultSavePath = settings.DefaultSavePath ?? settings.GetDefaultDownloadPath(),

                // فهرست انواع فایل
                InterceptableFileTypes = new List<string>(settings.InterceptableFileTypes),
                InterceptableMimeTypes = new List<string>(settings.InterceptableMimeTypes),
                InterceptUnknownFileTypes = settings.InterceptUnknownFileTypes,

                // فهرست استثناها
                ExcludedDomains = new List<string>(settings.ExcludedDomains),
                ExcludedUrlPatterns = new List<string>(settings.ExcludedUrlPatterns),

                // آمار
                TotalProcessedDownloads = stats.TotalProcessedDownloads,
                TotalInterceptedDownloads = stats.TotalInterceptedDownloads,
                InterceptionSuccessRate = stats.InterceptionSuccessRate,
                DownloadsByBrowser = new Dictionary<string, int>(stats.DownloadsByBrowser),
                InterceptedByBrowser = new Dictionary<string, int>(stats.InterceptedByBrowser),
                LastResetTime = stats.LastResetTime
            };
        }

        /// <summary>
        /// به‌روزرسانی تنظیمات از UI
        /// </summary>
        public void UpdateSettingsFromUI(DownloadInterceptionUISettings uiSettings)
        {
            if (uiSettings == null)
                throw new ArgumentNullException(nameof(uiSettings));

            var settings = _settingsManager.Settings;

            // به‌روزرسانی تنظیمات اصلی
            settings.EnableDownloadInterception = uiSettings.EnableDownloadInterception;
            settings.AutoInterceptAllDownloads = uiSettings.AutoInterceptAllDownloads;
            settings.InterceptLargeFilesOnly = uiSettings.InterceptLargeFilesOnly;
            settings.LargeFileThresholdMB = uiSettings.LargeFileThresholdMB;
            settings.MinFileSizeForInterception = (long)(uiSettings.MinFileSizeForInterceptionMB * 1024 * 1024);
            settings.MaxFileSizeForInterception = uiSettings.MaxFileSizeForInterceptionMB > 0 
                ? (long)(uiSettings.MaxFileSizeForInterceptionMB * 1024 * 1024) 
                : 0;

            // به‌روزرسانی تنظیمات پیشرفته
            settings.EnableAdvancedPriority = uiSettings.EnableAdvancedPriority;
            settings.EdgeHasHighestPriority = uiSettings.EdgeHasHighestPriority;
            settings.EnablePriorityQueue = uiSettings.EnablePriorityQueue;
            settings.InterceptionThreshold = uiSettings.InterceptionThreshold;

            // به‌روزرسانی تنظیمات اعلان‌ها
            settings.ShowInterceptionNotification = uiSettings.ShowInterceptionNotification;
            settings.InterceptionDelayMs = uiSettings.InterceptionDelayMs;

            // به‌روزرسانی مسیر ذخیره
            settings.DefaultSavePath = uiSettings.DefaultSavePath;

            // به‌روزرسانی فهرست انواع فایل
            settings.InterceptableFileTypes = new HashSet<string>(uiSettings.InterceptableFileTypes, StringComparer.OrdinalIgnoreCase);
            settings.InterceptableMimeTypes = new HashSet<string>(uiSettings.InterceptableMimeTypes, StringComparer.OrdinalIgnoreCase);
            settings.InterceptUnknownFileTypes = uiSettings.InterceptUnknownFileTypes;

            // به‌روزرسانی فهرست استثناها
            settings.ExcludedDomains = new HashSet<string>(uiSettings.ExcludedDomains, StringComparer.OrdinalIgnoreCase);
            settings.ExcludedUrlPatterns = new HashSet<string>(uiSettings.ExcludedUrlPatterns);

            // ذخیره تنظیمات
            _settingsManager.UpdateSettings(settings);
            _interceptionManager.UpdateSettings(settings);

            _logger.LogInfo("Download interception settings updated from UI");
        }

        /// <summary>
        /// فعال/غیرفعال کردن سریع رهگیری دانلود
        /// </summary>
        public void ToggleDownloadInterception()
        {
            var currentState = _settingsManager.Settings.EnableDownloadInterception;
            _settingsManager.SetDownloadInterceptionEnabled(!currentState);
            
            OnStatusChanged(new InterceptionStatusChangedEventArgs
            {
                IsEnabled = !currentState,
                Message = !currentState ? "رهگیری دانلود فعال شد" : "رهگیری دانلود غیرفعال شد"
            });

            _logger.LogInfo($"Download interception toggled: {(!currentState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// اضافه کردن نوع فایل جدید
        /// </summary>
        public void AddFileType(string fileType)
        {
            if (string.IsNullOrWhiteSpace(fileType))
                return;

            _settingsManager.AddInterceptableFileType(fileType);
            
            OnSettingsChanged(new InterceptionSettingsChangedEventArgs
            {
                ChangeType = "FileTypeAdded",
                ChangedValue = fileType,
                Message = $"نوع فایل '{fileType}' اضافه شد"
            });
        }

        /// <summary>
        /// حذف نوع فایل
        /// </summary>
        public void RemoveFileType(string fileType)
        {
            if (string.IsNullOrWhiteSpace(fileType))
                return;

            _settingsManager.RemoveInterceptableFileType(fileType);
            
            OnSettingsChanged(new InterceptionSettingsChangedEventArgs
            {
                ChangeType = "FileTypeRemoved",
                ChangedValue = fileType,
                Message = $"نوع فایل '{fileType}' حذف شد"
            });
        }

        /// <summary>
        /// اضافه کردن دامنه مستثنی
        /// </summary>
        public void AddExcludedDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return;

            _settingsManager.AddExcludedDomain(domain);
            
            OnSettingsChanged(new InterceptionSettingsChangedEventArgs
            {
                ChangeType = "DomainExcluded",
                ChangedValue = domain,
                Message = $"دامنه '{domain}' به فهرست استثناها اضافه شد"
            });
        }

        /// <summary>
        /// حذف دامنه مستثنی
        /// </summary>
        public void RemoveExcludedDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return;

            _settingsManager.RemoveExcludedDomain(domain);
            
            OnSettingsChanged(new InterceptionSettingsChangedEventArgs
            {
                ChangeType = "DomainIncluded",
                ChangedValue = domain,
                Message = $"دامنه '{domain}' از فهرست استثناها حذف شد"
            });
        }

        /// <summary>
        /// ریست آمار رهگیری
        /// </summary>
        public void ResetInterceptionStats()
        {
            _interceptionManager.ResetStats();
            
            OnStatusChanged(new InterceptionStatusChangedEventArgs
            {
                IsEnabled = _settingsManager.Settings.EnableDownloadInterception,
                Message = "آمار رهگیری دانلود ریست شد"
            });

            _logger.LogInfo("Download interception stats reset from UI");
        }

        /// <summary>
        /// تست تنظیمات با یک URL نمونه
        /// </summary>
        public InterceptionTestResult TestInterceptionSettings(string testUrl, string? fileName = null, long? fileSize = null)
        {
            if (string.IsNullOrWhiteSpace(testUrl))
                throw new ArgumentException("Test URL cannot be empty", nameof(testUrl));

            try
            {
                var context = new DownloadFilterContext
                {
                    Url = testUrl,
                    FileName = fileName,
                    FileSize = fileSize,
                    Browser = "test",
                    Settings = _settingsManager.Settings
                };

                var filterManager = _interceptionManager.GetFilterManager();
                var result = filterManager.ShouldInterceptDownload(context);

                return new InterceptionTestResult
                {
                    ShouldIntercept = result.ShouldIntercept,
                    Reason = result.Reason,
                    FilterName = result.FilterName,
                    TestUrl = testUrl,
                    TestFileName = fileName,
                    TestFileSize = fileSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error testing interception settings for URL: {testUrl}", ex);
                return new InterceptionTestResult
                {
                    ShouldIntercept = false,
                    Reason = $"خطا در تست: {ex.Message}",
                    FilterName = "Error",
                    TestUrl = testUrl,
                    TestFileName = fileName,
                    TestFileSize = fileSize
                };
            }
        }

        /// <summary>
        /// دریافت پیشنهادات بهینه‌سازی تنظیمات
        /// </summary>
        public List<InterceptionOptimizationSuggestion> GetOptimizationSuggestions()
        {
            var suggestions = new List<InterceptionOptimizationSuggestion>();
            var settings = _settingsManager.Settings;
            var stats = _interceptionManager.GetStats();

            // پیشنهاد بر اساس نرخ موفقیت
            if (stats.TotalProcessedDownloads > 10 && stats.InterceptionSuccessRate < 50)
            {
                suggestions.Add(new InterceptionOptimizationSuggestion
                {
                    Type = "LowSuccessRate",
                    Title = "نرخ موفقیت رهگیری پایین",
                    Description = $"نرخ موفقیت رهگیری شما {stats.InterceptionSuccessRate:F1}% است. ممکن است فیلترهای شما خیلی سخت‌گیرانه باشند.",
                    Recommendation = "حداقل اندازه فایل را کاهش دهید یا انواع فایل بیشتری را اضافه کنید.",
                    Priority = "Medium"
                });
            }

            // پیشنهاد برای فایل‌های کوچک
            if (settings.MinFileSizeForInterception > 10 * 1024 * 1024) // بیشتر از 10MB
            {
                suggestions.Add(new InterceptionOptimizationSuggestion
                {
                    Type = "HighMinFileSize",
                    Title = "حداقل اندازه فایل بالا",
                    Description = "حداقل اندازه فایل برای رهگیری بالا تنظیم شده است.",
                    Recommendation = "برای رهگیری فایل‌های کوچک‌تر، حداقل اندازه را کاهش دهید.",
                    Priority = "Low"
                });
            }

            // پیشنهاد برای فعال‌سازی اولویت‌بندی پیشرفته
            if (!settings.EnableAdvancedPriority && stats.TotalProcessedDownloads > 50)
            {
                suggestions.Add(new InterceptionOptimizationSuggestion
                {
                    Type = "EnableAdvancedPriority",
                    Title = "فعال‌سازی اولویت‌بندی پیشرفته",
                    Description = "با توجه به تعداد دانلودهای شما، اولویت‌بندی پیشرفته می‌تواند مفید باشد.",
                    Recommendation = "اولویت‌بندی پیشرفته را فعال کنید تا کیفیت رهگیری بهبود یابد.",
                    Priority = "High"
                });
            }

            return suggestions;
        }

        /// <summary>
        /// نمایش اعلان رهگیری دانلود
        /// </summary>
        public void ShowInterceptionNotification(string title, string message, InterceptionNotificationType type = InterceptionNotificationType.Info)
        {
            if (!_settingsManager.Settings.ShowInterceptionNotification)
                return;

            OnNotificationRequested(new InterceptionNotificationEventArgs
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Event handler برای تغییر تنظیمات
        /// </summary>
        private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            OnSettingsChanged(new InterceptionSettingsChangedEventArgs
            {
                ChangeType = "SettingsUpdated",
                Message = "تنظیمات رهگیری دانلود به‌روزرسانی شد"
            });
        }

        /// <summary>
        /// Event handler برای رهگیری دانلود
        /// </summary>
        private void OnDownloadIntercepted(object? sender, DownloadInterceptedEventArgs e)
        {
            ShowInterceptionNotification(
                "🔄 دانلود رهگیری شد",
                $"فایل {e.DownloadItem.FileName} از {e.Browser} رهگیری شد",
                InterceptionNotificationType.Success
            );
        }

        /// <summary>
        /// Event handler برای پردازش دانلود
        /// </summary>
        private void OnDownloadProcessed(object? sender, DownloadProcessedEventArgs e)
        {
            // اختیاری: نمایش اعلان برای دانلودهای پردازش شده
        }

        protected virtual void OnSettingsChanged(InterceptionSettingsChangedEventArgs e)
        {
            SettingsChanged?.Invoke(this, e);
        }

        protected virtual void OnStatusChanged(InterceptionStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        protected virtual void OnNotificationRequested(InterceptionNotificationEventArgs e)
        {
            NotificationRequested?.Invoke(this, e);
        }
    }

    /// <summary>
    /// تنظیمات رهگیری دانلود برای UI
    /// </summary>
    public class DownloadInterceptionUISettings
    {
        // تنظیمات اصلی
        public bool EnableDownloadInterception { get; set; } = true;
        public bool AutoInterceptAllDownloads { get; set; } = false;
        public bool InterceptLargeFilesOnly { get; set; } = true;
        public int LargeFileThresholdMB { get; set; } = 10;
        public double MinFileSizeForInterceptionMB { get; set; } = 0.1;
        public double MaxFileSizeForInterceptionMB { get; set; } = 0;

        // تنظیمات پیشرفته
        public bool EnableAdvancedPriority { get; set; } = true;
        public bool EdgeHasHighestPriority { get; set; } = true;
        public bool EnablePriorityQueue { get; set; } = true;
        public double InterceptionThreshold { get; set; } = 0.5;

        // تنظیمات اعلان‌ها
        public bool ShowInterceptionNotification { get; set; } = true;
        public int InterceptionDelayMs { get; set; } = 500;

        // مسیر ذخیره
        public string DefaultSavePath { get; set; } = "";

        // فهرست انواع فایل
        public List<string> InterceptableFileTypes { get; set; } = new();
        public List<string> InterceptableMimeTypes { get; set; } = new();
        public bool InterceptUnknownFileTypes { get; set; } = false;

        // فهرست استثناها
        public List<string> ExcludedDomains { get; set; } = new();
        public List<string> ExcludedUrlPatterns { get; set; } = new();

        // آمار (فقط خواندنی)
        public int TotalProcessedDownloads { get; set; } = 0;
        public int TotalInterceptedDownloads { get; set; } = 0;
        public double InterceptionSuccessRate { get; set; } = 0;
        public Dictionary<string, int> DownloadsByBrowser { get; set; } = new();
        public Dictionary<string, int> InterceptedByBrowser { get; set; } = new();
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// نتیجه تست تنظیمات رهگیری
    /// </summary>
    public class InterceptionTestResult
    {
        public bool ShouldIntercept { get; set; }
        public string Reason { get; set; } = "";
        public string FilterName { get; set; } = "";
        public string TestUrl { get; set; } = "";
        public string? TestFileName { get; set; }
        public long? TestFileSize { get; set; }
    }

    /// <summary>
    /// پیشنهاد بهینه‌سازی تنظیمات
    /// </summary>
    public class InterceptionOptimizationSuggestion
    {
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public string Priority { get; set; } = "Low"; // Low, Medium, High
    }

    /// <summary>
    /// نوع اعلان رهگیری
    /// </summary>
    public enum InterceptionNotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Event Args برای تغییر تنظیمات رهگیری
    /// </summary>
    public class InterceptionSettingsChangedEventArgs : EventArgs
    {
        public string ChangeType { get; set; } = "";
        public string? ChangedValue { get; set; }
        public string Message { get; set; } = "";
        public DateTime ChangeTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event Args برای تغییر وضعیت رهگیری
    /// </summary>
    public class InterceptionStatusChangedEventArgs : EventArgs
    {
        public bool IsEnabled { get; set; }
        public string Message { get; set; } = "";
        public DateTime ChangeTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event Args برای درخواست نمایش اعلان
    /// </summary>
    public class InterceptionNotificationEventArgs : EventArgs
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public InterceptionNotificationType Type { get; set; } = InterceptionNotificationType.Info;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}