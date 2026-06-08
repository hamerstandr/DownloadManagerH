using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// فیلترهای پیشرفته برای رهگیری دانلود
    /// </summary>
    public class AdvancedDownloadFilters
    {
        private readonly ILogger _logger;
        private readonly List<IAdvancedDownloadFilter> _filters;

        public AdvancedDownloadFilters(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filters = new List<IAdvancedDownloadFilter>();

            InitializeAdvancedFilters();
        }

        /// <summary>
        /// مقداردهی فیلترهای پیشرفته
        /// </summary>
        private void InitializeAdvancedFilters()
        {
            // فیلتر اولویت مرورگر
            AddFilter(new BrowserPriorityFilter());
            
            // فیلتر زمان‌بندی
            AddFilter(new TimeBasedFilter());
            
            // فیلتر محتوای فایل
            AddFilter(new ContentTypeFilter());
            
            // فیلتر منطقه جغرافیایی
            AddFilter(new GeographicFilter());
            
            // فیلتر کیفیت اتصال
            AddFilter(new ConnectionQualityFilter());
            
            // فیلتر تاریخچه دانلود
            AddFilter(new DownloadHistoryFilter());

            _logger.LogInfo("Advanced download filters initialized");
        }

        /// <summary>
        /// اضافه کردن فیلتر پیشرفته
        /// </summary>
        public void AddFilter(IAdvancedDownloadFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            _filters.Add(filter);
            _logger.LogDebug($"Added advanced filter: {filter.Name}");
        }

        /// <summary>
        /// بررسی پیشرفته رهگیری دانلود
        /// </summary>
        public AdvancedFilterResult ShouldInterceptDownload(AdvancedFilterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var results = new List<AdvancedFilterResult>();
            var totalScore = 0.0;
            var maxScore = 0.0;

            foreach (var filter in _filters.Where(f => f.IsEnabled))
            {
                try
                {
                    var result = filter.Evaluate(context);
                    results.Add(result);
                    
                    totalScore += result.Score * filter.Weight;
                    maxScore += filter.Weight;

                    // اگر فیلتر مسدودکننده باشد و نتیجه منفی باشد
                    if (filter.IsBlocking && result.Score <= 0)
                    {
                        return new AdvancedFilterResult
                        {
                            ShouldIntercept = false,
                            Score = 0,
                            Reason = result.Reason,
                            FilterName = filter.Name,
                            Confidence = 1.0
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in advanced filter {filter.Name}", ex);
                }
            }

            // محاسبه نتیجه نهایی
            var finalScore = maxScore > 0 ? totalScore / maxScore : 0;
            var shouldIntercept = finalScore >= context.Settings.InterceptionThreshold;

            return new AdvancedFilterResult
            {
                ShouldIntercept = shouldIntercept,
                Score = finalScore,
                Reason = shouldIntercept ? "Passed advanced filtering" : "Failed advanced filtering",
                FilterName = "Combined",
                Confidence = CalculateConfidence(results),
                DetailedResults = results
            };
        }

        /// <summary>
        /// محاسبه اعتماد نتیجه
        /// </summary>
        private static double CalculateConfidence(List<AdvancedFilterResult> results)
        {
            if (!results.Any())
                return 0;

            var avgConfidence = results.Average(r => r.Confidence);
            var scoreVariance = CalculateVariance(results.Select(r => r.Score));
            
            // اعتماد کمتر اگر تنوع زیاد باشد
            return avgConfidence * (1.0 - Math.Min(scoreVariance, 0.5));
        }

        /// <summary>
        /// محاسبه واریانس
        /// </summary>
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count <= 1)
                return 0;

            var mean = valuesList.Average();
            var variance = valuesList.Sum(v => Math.Pow(v - mean, 2)) / valuesList.Count;
            return variance;
        }

        /// <summary>
        /// دریافت فیلترهای فعال
        /// </summary>
        public List<IAdvancedDownloadFilter> GetActiveFilters()
        {
            return _filters.Where(f => f.IsEnabled).ToList();
        }
    }

    /// <summary>
    /// رابط فیلتر پیشرفته دانلود
    /// </summary>
    public interface IAdvancedDownloadFilter
    {
        string Name { get; }
        bool IsEnabled { get; set; }
        bool IsBlocking { get; }
        double Weight { get; set; }
        AdvancedFilterResult Evaluate(AdvancedFilterContext context);
    }

    /// <summary>
    /// کنتکست فیلتر پیشرفته
    /// </summary>
    public class AdvancedFilterContext : DownloadFilterContext
    {
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
        public string UserAgent { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
        public new DownloadInterceptionSettings Settings { get; set; } = new();
        public List<DownloadItem> RecentDownloads { get; set; } = new();
        public NetworkConnectionInfo ConnectionInfo { get; set; } = new();
    }

    /// <summary>
    /// نتیجه فیلتر پیشرفته
    /// </summary>
    public class AdvancedFilterResult : FilterResult
    {
        public double Score { get; set; } = 0;
        public double Confidence { get; set; } = 1.0;
        public List<AdvancedFilterResult> DetailedResults { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// اطلاعات اتصال شبکه
    /// </summary>
    public class NetworkConnectionInfo
    {
        public string ConnectionType { get; set; } = "unknown"; // wifi, ethernet, mobile
        public double BandwidthMbps { get; set; } = 0;
        public int LatencyMs { get; set; } = 0;
        public bool IsMetered { get; set; } = false;
        public string Location { get; set; } = "";
    }

    /// <summary>
    /// فیلتر اولویت مرورگر
    /// </summary>
    public class BrowserPriorityFilter : IAdvancedDownloadFilter
    {
        public string Name => "Browser Priority Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false;
        public double Weight { get; set; } = 1.0;

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            var priority = NativeMessagingProtocol.GetBrowserPriority(context.Browser);
            var score = (int)priority / 4.0; // نرمال‌سازی به 0-1

            return new AdvancedFilterResult
            {
                ShouldIntercept = score > 0.25,
                Score = score,
                Reason = $"Browser {context.Browser} has {priority} priority",
                FilterName = Name,
                Confidence = 0.9
            };
        }
    }

    /// <summary>
    /// فیلتر زمان‌بندی
    /// </summary>
    public class TimeBasedFilter : IAdvancedDownloadFilter
    {
        public string Name => "Time-Based Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false;
        public double Weight { get; set; } = 0.5;

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            var hour = context.RequestTime.Hour;
            
            // اولویت بیشتر در ساعات کاری (9-17)
            var score = hour >= 9 && hour <= 17 ? 0.8 : 0.4;
            
            // اولویت کمتر در ساعات شلوغ شبکه (19-23)
            if (hour >= 19 && hour <= 23)
                score *= 0.7;

            return new AdvancedFilterResult
            {
                ShouldIntercept = score > 0.3,
                Score = score,
                Reason = $"Time-based score for hour {hour}",
                FilterName = Name,
                Confidence = 0.6
            };
        }
    }

    /// <summary>
    /// فیلتر نوع محتوا
    /// </summary>
    public class ContentTypeFilter : IAdvancedDownloadFilter
    {
        public string Name => "Content Type Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false;
        public double Weight { get; set; } = 1.2;

        private readonly Dictionary<string, double> _contentScores = new()
        {
            { "application/zip", 0.9 },
            { "application/x-rar", 0.9 },
            { "application/pdf", 0.7 },
            { "video/", 0.8 },
            { "audio/", 0.6 },
            { "image/", 0.3 },
            { "text/", 0.2 }
        };

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            if (string.IsNullOrEmpty(context.MimeType))
            {
                return new AdvancedFilterResult
                {
                    ShouldIntercept = false,
                    Score = 0.1,
                    Reason = "Unknown content type",
                    FilterName = Name,
                    Confidence = 0.3
                };
            }

            var score = _contentScores
                .Where(kvp => context.MimeType.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value)
                .FirstOrDefault(0.1);

            return new AdvancedFilterResult
            {
                ShouldIntercept = score > 0.4,
                Score = score,
                Reason = $"Content type {context.MimeType} scored {score:F2}",
                FilterName = Name,
                Confidence = 0.8
            };
        }
    }

    /// <summary>
    /// فیلتر منطقه جغرافیایی
    /// </summary>
    public class GeographicFilter : IAdvancedDownloadFilter
    {
        public string Name => "Geographic Filter";
        public bool IsEnabled { get; set; } = false; // غیرفعال به طور پیش‌فرض
        public bool IsBlocking => false;
        public double Weight { get; set; } = 0.3;

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            // پیاده‌سازی ساده - می‌تواند با سرویس‌های geolocation بهبود یابد
            var score = 0.5; // نمره متوسط برای همه مناطق

            return new AdvancedFilterResult
            {
                ShouldIntercept = true,
                Score = score,
                Reason = "Geographic filtering not implemented",
                FilterName = Name,
                Confidence = 0.1
            };
        }
    }

    /// <summary>
    /// فیلتر کیفیت اتصال
    /// </summary>
    public class ConnectionQualityFilter : IAdvancedDownloadFilter
    {
        public string Name => "Connection Quality Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false;
        public double Weight { get; set; } = 0.8;

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            var connection = context.ConnectionInfo;
            var score = 0.5; // نمره پایه

            // بهبود نمره بر اساس پهنای باند
            if (connection.BandwidthMbps > 50)
                score += 0.3;
            else if (connection.BandwidthMbps > 10)
                score += 0.1;

            // کاهش نمره برای اتصالات محدود
            if (connection.IsMetered)
                score -= 0.2;

            // کاهش نمره برای تأخیر بالا
            if (connection.LatencyMs > 200)
                score -= 0.2;

            score = Math.Max(0, Math.Min(1, score));

            return new AdvancedFilterResult
            {
                ShouldIntercept = score > 0.4,
                Score = score,
                Reason = $"Connection quality score: {score:F2}",
                FilterName = Name,
                Confidence = 0.7
            };
        }
    }

    /// <summary>
    /// فیلتر تاریخچه دانلود
    /// </summary>
    public class DownloadHistoryFilter : IAdvancedDownloadFilter
    {
        public string Name => "Download History Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false;
        public double Weight { get; set; } = 0.6;

        public AdvancedFilterResult Evaluate(AdvancedFilterContext context)
        {
            var recentDownloads = context.RecentDownloads
                .Where(d => d.Url == context.Url || d.FileName == context.FileName)
                .ToList();

            if (recentDownloads.Any())
            {
                // کاهش اولویت برای دانلودهای تکراری
                return new AdvancedFilterResult
                {
                    ShouldIntercept = false,
                    Score = 0.1,
                    Reason = "Duplicate download detected",
                    FilterName = Name,
                    Confidence = 0.9
                };
            }

            // بررسی الگوی دانلود کاربر
            var userPattern = AnalyzeUserPattern(context.RecentDownloads);
            var score = userPattern.MatchesCurrentDownload(context) ? 0.8 : 0.4;

            return new AdvancedFilterResult
            {
                ShouldIntercept = score > 0.5,
                Score = score,
                Reason = $"User pattern analysis score: {score:F2}",
                FilterName = Name,
                Confidence = 0.6
            };
        }

        private UserDownloadPattern AnalyzeUserPattern(List<DownloadItem> recentDownloads)
        {
            // تحلیل ساده الگوی دانلود کاربر
            return new UserDownloadPattern
            {
                PreferredFileTypes = recentDownloads
                    .GroupBy(d => System.IO.Path.GetExtension(d.FileName))
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList(),
                AverageFileSize = recentDownloads.Any() ? recentDownloads.Average(d => d.TotalBytes) : 0,
                PreferredTimeOfDay = recentDownloads.Any() ? recentDownloads.Average(d => d.ScheduledTime?.Hour ?? DateTime.Now.Hour) : 12
            };
        }
    }

    /// <summary>
    /// الگوی دانلود کاربر
    /// </summary>
    public class UserDownloadPattern
    {
        public List<string> PreferredFileTypes { get; set; } = new();
        public double AverageFileSize { get; set; }
        public double PreferredTimeOfDay { get; set; }

        public bool MatchesCurrentDownload(AdvancedFilterContext context)
        {
            var fileExtension = System.IO.Path.GetExtension(context.FileName ?? "");
            var currentHour = context.RequestTime.Hour;
            
            var typeMatch = PreferredFileTypes.Contains(fileExtension);
            var timeMatch = Math.Abs(currentHour - PreferredTimeOfDay) <= 2;
            var sizeMatch = context.FileSize.HasValue && 
                           Math.Abs(context.FileSize.Value - AverageFileSize) / AverageFileSize <= 0.5;

            return typeMatch || (timeMatch && sizeMatch);
        }
    }
}