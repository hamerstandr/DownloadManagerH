using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت رهگیری و اولویت‌بندی دانلودها از مرورگرهای مختلف
    /// </summary>
    public class DownloadInterceptionManager
    {
        private readonly DownloadManager _downloadManager;
        private readonly ILogger _logger;
        private readonly DownloadFilterManager _filterManager;
        private readonly AdvancedDownloadFilters _advancedFilters;
        private readonly object _lockObject = new object();

        // تنظیمات رهگیری دانلود
        public DownloadInterceptionSettings Settings { get; private set; }

        // آمار رهگیری دانلود
        public DownloadInterceptionStats Stats { get; private set; }

        // رویدادها
        public event EventHandler<DownloadInterceptedEventArgs>? DownloadIntercepted;
        public event EventHandler<DownloadProcessedEventArgs>? DownloadProcessed;

        public DownloadInterceptionManager(DownloadManager downloadManager, ILogger logger)
        {
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filterManager = new DownloadFilterManager(_logger);
            _advancedFilters = new AdvancedDownloadFilters(_logger);
            
            Settings = new DownloadInterceptionSettings();
            Stats = new DownloadInterceptionStats();

            _logger.LogInfo("Download Interception Manager initialized with advanced filters");
        }

        /// <summary>
        /// پردازش پیام اضافه کردن دانلود با در نظر گیری اولویت مرورگر
        /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<DownloadProcessingResult> ProcessAddDownloadMessage(AddDownloadMessage message)
#pragma warning restore CS1998
        {
            if (message?.Data?.Links == null || message.Data.Links.Count == 0)
            {
                return new DownloadProcessingResult
                {
                    Success = false,
                    Message = "هیچ لینک دانلودی ارائه نشده است",
                    ProcessedCount = 0
                };
            }

            try
            {
                var browserPriority = NativeMessagingProtocol.GetBrowserPriority(message.Browser);
                var processedLinks = new List<string>();
                var errors = new List<string>();

                _logger.LogInfo($"Processing {message.Data.Links.Count} download links from {message.Browser} (Priority: {browserPriority})");

                foreach (var linkData in message.Data.Links)
                {
                    try
                    {
                        // اعتبارسنجی لینک
                        var validationResult = NativeMessagingProtocol.ValidateDownloadLink(linkData);
                        if (!validationResult.IsValid)
                        {
                            errors.AddRange(validationResult.Errors);
                            continue;
                        }

                        // تبدیل به DownloadItem
                        var downloadItem = CreateDownloadItemFromLinkData(linkData, message, browserPriority);
                        
                        // اضافه کردن به مدیر دانلود
                        _downloadManager.AddDownload(downloadItem);
                        var addResult = true; // فرض موفقیت
                        
                        if (addResult)
                        {
                            processedLinks.Add(linkData.Url);
                            
                            // آپدیت آمار
                            lock (_lockObject)
                            {
                                Stats.TotalProcessedDownloads++;
                                Stats.DownloadsByBrowser[message.Browser] = Stats.DownloadsByBrowser.GetValueOrDefault(message.Browser, 0) + 1;
                            }

                            // رویداد پردازش دانلود
                            OnDownloadProcessed(new DownloadProcessedEventArgs
                            {
                                DownloadItem = downloadItem,
                                Browser = message.Browser,
                                Priority = browserPriority,
                                ProcessingTime = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            errors.Add($"خطا در اضافه کردن دانلود: {linkData.Url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing download link: {linkData.Url}", ex);
                        errors.Add($"خطا در پردازش لینک {linkData.Url}: {ex.Message}");
                    }
                }

                var result = new DownloadProcessingResult
                {
                    Success = processedLinks.Count > 0,
                    Message = processedLinks.Count > 0 
                        ? $"{processedLinks.Count} دانلود با موفقیت اضافه شد"
                        : "هیچ دانلودی اضافه نشد",
                    ProcessedCount = processedLinks.Count,
                    ErrorCount = errors.Count,
                    Errors = errors,
                    ProcessedUrls = processedLinks
                };

                _logger.LogInfo($"Processed {processedLinks.Count} downloads successfully, {errors.Count} errors from {message.Browser}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in ProcessAddDownloadMessage", ex);
                return new DownloadProcessingResult
                {
                    Success = false,
                    Message = $"خطا در پردازش درخواست: {ex.Message}",
                    ProcessedCount = 0,
                    ErrorCount = 1,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// پردازش پیام رهگیری دانلود
        /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<DownloadProcessingResult> ProcessInterceptDownloadMessage(InterceptDownloadMessage message)
#pragma warning restore CS1998
        {
            if (message?.Data == null)
            {
                return new DownloadProcessingResult
                {
                    Success = false,
                    Message = "داده‌های رهگیری دانلود معتبر نیست",
                    ProcessedCount = 0
                };
            }

            try
            {
                var browserPriority = NativeMessagingProtocol.GetBrowserPriority(message.Browser);
                
                // بررسی اینکه آیا این نوع فایل باید رهگیری شود
                if (!ShouldInterceptDownload(message.Data, message.Browser))
                {
                    _logger.LogDebug($"Download not intercepted (filtered): {message.Data.Url}");
                    return new DownloadProcessingResult
                    {
                        Success = false,
                        Message = "این نوع فایل برای رهگیری انتخاب نشده است",
                        ProcessedCount = 0
                    };
                }

                // تبدیل به DownloadLinkData
                var linkData = new DownloadLinkData
                {
                    Url = message.Data.Url,
                    Filename = message.Data.Filename,
                    TotalBytes = message.Data.TotalBytes,
                    MimeType = message.Data.MimeType,
                    Referrer = message.Data.Referrer,
                    Intercepted = true
                };

                // ایجاد DownloadItem
                var downloadItem = CreateDownloadItemFromLinkData(linkData, message, browserPriority);
                
                // اضافه کردن به مدیر دانلود
                _downloadManager.AddDownload(downloadItem);
                var addResult = true; // فرض موفقیت
                
                if (addResult)
                {
                    // آپدیت آمار
                    lock (_lockObject)
                    {
                        Stats.TotalInterceptedDownloads++;
                        Stats.InterceptedByBrowser[message.Browser] = Stats.InterceptedByBrowser.GetValueOrDefault(message.Browser, 0) + 1;
                    }

                    // رویداد رهگیری دانلود
                    OnDownloadIntercepted(new DownloadInterceptedEventArgs
                    {
                        DownloadItem = downloadItem,
                        Browser = message.Browser,
                        Priority = browserPriority,
                        OriginalDownloadId = message.Data.DownloadId,
                        InterceptionTime = DateTime.UtcNow
                    });

                    _logger.LogInfo($"Successfully intercepted download from {message.Browser}: {message.Data.Url}");
                    
                    return new DownloadProcessingResult
                    {
                        Success = true,
                        Message = "دانلود با موفقیت رهگیری شد",
                        ProcessedCount = 1,
                        ProcessedUrls = new List<string> { message.Data.Url }
                    };
                }
                else
                {
                    return new DownloadProcessingResult
                    {
                        Success = false,
                        Message = "خطا در اضافه کردن دانلود رهگیری شده",
                        ProcessedCount = 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in ProcessInterceptDownloadMessage", ex);
                return new DownloadProcessingResult
                {
                    Success = false,
                    Message = $"خطا در پردازش رهگیری دانلود: {ex.Message}",
                    ProcessedCount = 0,
                    ErrorCount = 1,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// تعیین اینکه آیا دانلود باید رهگیری شود
        /// </summary>
        private bool ShouldInterceptDownload(InterceptDownloadData downloadData, string browser = "unknown")
        {
            if (!Settings.EnableDownloadInterception)
                return false;

            // بررسی رهگیری خودکار
            if (Settings.AutoInterceptAllDownloads)
                return true;

            // بررسی رهگیری فقط فایل‌های بزرگ
            if (Settings.InterceptLargeFilesOnly && downloadData.TotalBytes.HasValue)
            {
                var fileSizeMB = downloadData.TotalBytes.Value / (1024.0 * 1024.0);
                if (fileSizeMB < Settings.LargeFileThresholdMB)
                {
                    _logger.LogDebug($"File too small for large-files-only mode: {fileSizeMB:F2}MB < {Settings.LargeFileThresholdMB}MB");
                    return false;
                }
            }

            // ایجاد کنتکست فیلتر پایه
            var basicContext = new DownloadFilterContext
            {
                Url = downloadData.Url,
                FileName = downloadData.Filename,
                FileSize = downloadData.TotalBytes,
                MimeType = downloadData.MimeType,
                Referrer = downloadData.Referrer,
                Browser = browser,
                Settings = Settings
            };

            // استفاده از فیلتر منیجر پایه
            var basicResult = _filterManager.ShouldInterceptDownload(basicContext);
            
            if (!basicResult.ShouldIntercept)
            {
                _logger.LogDebug($"Download not intercepted by basic filters: {basicResult.Reason} (Filter: {basicResult.FilterName})");
                return false;
            }

            // اگر فیلترهای پیشرفته فعال باشند
            if (Settings.EnableAdvancedPriority)
            {
                var advancedContext = new AdvancedFilterContext
                {
                    Url = downloadData.Url,
                    FileName = downloadData.Filename,
                    FileSize = downloadData.TotalBytes,
                    MimeType = downloadData.MimeType,
                    Referrer = downloadData.Referrer,
                    Browser = browser,
                    Settings = Settings,
                    RequestTime = DateTime.UtcNow,
                    RecentDownloads = GetRecentDownloads(),
                    ConnectionInfo = GetConnectionInfo()
                };

                var advancedResult = _advancedFilters.ShouldInterceptDownload(advancedContext);
                
                if (!advancedResult.ShouldIntercept)
                {
                    _logger.LogDebug($"Download not intercepted by advanced filters: {advancedResult.Reason} (Score: {advancedResult.Score:F2}, Confidence: {advancedResult.Confidence:F2})");
                    return false;
                }

                _logger.LogDebug($"Download approved by advanced filters: Score {advancedResult.Score:F2}, Confidence {advancedResult.Confidence:F2}");
            }

            return true;
        }

        /// <summary>
        /// ایجاد DownloadItem از DownloadLinkData
        /// </summary>
        private DownloadItem CreateDownloadItemFromLinkData(DownloadLinkData linkData, NativeMessage message, NativeMessagingProtocol.BrowserPriority priority)
        {
            var downloadItem = new DownloadItem
            {
                Url = linkData.Url,
                FileName = linkData.Filename ?? ExtractFileNameFromUrl(linkData.Url),
                SavePath = Settings.DefaultSavePath ?? Settings.GetDefaultDownloadPath(),
                Referrer = linkData.Referrer ?? "",
                TotalBytes = linkData.TotalBytes ?? 0,
                Status = DownloadStatus.Pending,
                Priority = ConvertBrowserPriorityToDownloadPriority(priority)
            };

            // اضافه کردن headers اگر موجود باشد
            if (linkData.Headers != null)
            {
                foreach (var header in linkData.Headers)
                {
                    downloadItem.Headers[header.Key] = header.Value;
                }
            }

            // اضافه کردن cookies اگر موجود باشد
            if (!string.IsNullOrEmpty(linkData.Cookies))
            {
                downloadItem.Cookies.Add(linkData.Cookies);
            }

            return downloadItem;
        }

        /// <summary>
        /// استخراج نام فایل از URL
        /// </summary>
        private static string ExtractFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                return string.IsNullOrEmpty(fileName) ? "download" : fileName;
            }
            catch
            {
                return "download";
            }
        }

        /// <summary>
        /// دریافت User Agent مناسب برای مرورگر
        /// </summary>
        private static string GetUserAgentForBrowser(string browser)
        {
            return browser?.ToLower() switch
            {
                "edge" => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
                "chrome" => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "firefox" => "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
                _ => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            };
        }

        /// <summary>
        /// تبدیل اولویت مرورگر به اولویت دانلود
        /// </summary>
        private static DownloadPriority ConvertBrowserPriorityToDownloadPriority(NativeMessagingProtocol.BrowserPriority browserPriority)
        {
            return browserPriority switch
            {
                NativeMessagingProtocol.BrowserPriority.Critical => DownloadPriority.High,
                NativeMessagingProtocol.BrowserPriority.High => DownloadPriority.High,
                NativeMessagingProtocol.BrowserPriority.Normal => DownloadPriority.Normal,
                NativeMessagingProtocol.BrowserPriority.Low => DownloadPriority.Low,
                _ => DownloadPriority.Normal
            };
        }

        /// <summary>
        /// به‌روزرسانی تنظیمات رهگیری دانلود
        /// </summary>
        public void UpdateSettings(DownloadInterceptionSettings newSettings)
        {
            Settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            _logger.LogInfo("Download interception settings updated");
        }

        /// <summary>
        /// دریافت دانلودهای اخیر برای تحلیل الگو
        /// </summary>
        private List<DownloadItem> GetRecentDownloads()
        {
            try
            {
                // دریافت دانلودهای 24 ساعت گذشته
                var since = DateTime.UtcNow.AddHours(-24);
                return _downloadManager.Downloads
                    .Where(d => d.ScheduledTime >= since || (d.ScheduledTime == null && d.Status != DownloadStatus.Pending))
                    .OrderByDescending(d => d.ScheduledTime)
                    .Take(50)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting recent downloads", ex);
                return new List<DownloadItem>();
            }
        }

        /// <summary>
        /// دریافت اطلاعات اتصال شبکه
        /// </summary>
        private NetworkConnectionInfo GetConnectionInfo()
        {
            try
            {
                // پیاده‌سازی ساده - می‌تواند با API های ویندوز بهبود یابد
                return new NetworkConnectionInfo
                {
                    ConnectionType = "unknown",
                    BandwidthMbps = 100, // فرض پهنای باند متوسط
                    LatencyMs = 50,      // فرض تأخیر متوسط
                    IsMetered = false,   // فرض اتصال نامحدود
                    Location = "unknown"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting connection info", ex);
                return new NetworkConnectionInfo();
            }
        }

        /// <summary>
        /// دریافت فیلتر منیجر برای دسترسی خارجی
        /// </summary>
        public DownloadFilterManager GetFilterManager()
        {
            return _filterManager;
        }

        /// <summary>
        /// دریافت فیلترهای پیشرفته برای دسترسی خارجی
        /// </summary>
        public AdvancedDownloadFilters GetAdvancedFilters()
        {
            return _advancedFilters;
        }

        /// <summary>
        /// دریافت آمار رهگیری دانلود
        /// </summary>
        public DownloadInterceptionStats GetStats()
        {
            lock (_lockObject)
            {
                return new DownloadInterceptionStats
                {
                    TotalProcessedDownloads = Stats.TotalProcessedDownloads,
                    TotalInterceptedDownloads = Stats.TotalInterceptedDownloads,
                    DownloadsByBrowser = new Dictionary<string, int>(Stats.DownloadsByBrowser),
                    InterceptedByBrowser = new Dictionary<string, int>(Stats.InterceptedByBrowser),
                    LastResetTime = Stats.LastResetTime
                };
            }
        }

        /// <summary>
        /// ریست آمار
        /// </summary>
        public void ResetStats()
        {
            lock (_lockObject)
            {
                Stats = new DownloadInterceptionStats();
                _logger.LogInfo("Download interception stats reset");
            }
        }

        protected virtual void OnDownloadIntercepted(DownloadInterceptedEventArgs e)
        {
            DownloadIntercepted?.Invoke(this, e);
        }

        protected virtual void OnDownloadProcessed(DownloadProcessedEventArgs e)
        {
            DownloadProcessed?.Invoke(this, e);
        }
    }
}