using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت فیلترها و استثناهای رهگیری دانلود
    /// </summary>
    public class DownloadFilterManager
    {
        private readonly ILogger _logger;
        private readonly List<IDownloadFilter> _filters;

        public DownloadFilterManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filters = new List<IDownloadFilter>();

            // اضافه کردن فیلترهای پیش‌فرض
            InitializeDefaultFilters();
            
            _logger.LogInfo("Download Filter Manager initialized");
        }

        /// <summary>
        /// مقداردهی فیلترهای پیش‌فرض
        /// </summary>
        private void InitializeDefaultFilters()
        {
            // فیلتر اندازه فایل
            AddFilter(new FileSizeFilter());
            
            // فیلتر نوع فایل
            AddFilter(new FileTypeFilter());
            
            // فیلتر دامنه
            AddFilter(new DomainFilter());
            
            // فیلتر URL pattern
            AddFilter(new UrlPatternFilter());
            
            // فیلتر MIME type
            AddFilter(new MimeTypeFilter());
        }

        /// <summary>
        /// اضافه کردن فیلتر جدید
        /// </summary>
        public void AddFilter(IDownloadFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            _filters.Add(filter);
            _logger.LogDebug($"Added filter: {filter.GetType().Name}");
        }

        /// <summary>
        /// حذف فیلتر
        /// </summary>
        public void RemoveFilter(IDownloadFilter filter)
        {
            if (filter != null && _filters.Remove(filter))
            {
                _logger.LogDebug($"Removed filter: {filter.GetType().Name}");
            }
        }

        /// <summary>
        /// بررسی اینکه آیا دانلود باید رهگیری شود
        /// </summary>
        public FilterResult ShouldInterceptDownload(DownloadFilterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var results = new List<FilterResult>();

            foreach (var filter in _filters)
            {
                try
                {
                    var result = filter.ShouldIntercept(context);
                    results.Add(result);

                    // اگر فیلتر مسدودکننده باشد و نتیجه منفی باشد، بلافاصله false برگردان
                    if (filter.IsBlocking && !result.ShouldIntercept)
                    {
                        _logger.LogDebug($"Download blocked by filter {filter.GetType().Name}: {result.Reason}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in filter {filter.GetType().Name}", ex);
                    // در صورت خطا در فیلتر، ادامه بده
                }
            }

            // اگر همه فیلترها موافق باشند یا هیچ فیلتر مسدودکننده‌ای نباشد
            var positiveResults = results.Where(r => r.ShouldIntercept).ToList();
            var negativeResults = results.Where(r => !r.ShouldIntercept).ToList();

            if (positiveResults.Any())
            {
                return new FilterResult
                {
                    ShouldIntercept = true,
                    Reason = $"Approved by {positiveResults.Count} filter(s)",
                    FilterName = "Combined"
                };
            }
            else if (negativeResults.Any())
            {
                var firstNegative = negativeResults.First();
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = firstNegative.Reason,
                    FilterName = firstNegative.FilterName
                };
            }

            // پیش‌فرض: رهگیری نکن
            return new FilterResult
            {
                ShouldIntercept = false,
                Reason = "No matching filters",
                FilterName = "Default"
            };
        }

        /// <summary>
        /// دریافت فهرست فیلترهای فعال
        /// </summary>
        public List<IDownloadFilter> GetActiveFilters()
        {
            return new List<IDownloadFilter>(_filters);
        }

        /// <summary>
        /// فعال/غیرفعال کردن فیلتر خاص
        /// </summary>
        public void SetFilterEnabled(Type filterType, bool enabled)
        {
            var filter = _filters.FirstOrDefault(f => f.GetType() == filterType);
            if (filter != null)
            {
                filter.IsEnabled = enabled;
                _logger.LogInfo($"Filter {filterType.Name} {(enabled ? "enabled" : "disabled")}");
            }
        }
    }

    /// <summary>
    /// رابط فیلتر دانلود
    /// </summary>
    public interface IDownloadFilter
    {
        /// <summary>
        /// نام فیلتر
        /// </summary>
        string Name { get; }

        /// <summary>
        /// فعال/غیرفعال بودن فیلتر
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// آیا این فیلتر مسدودکننده است (اگر false برگرداند، دانلود رهگیری نمی‌شود)
        /// </summary>
        bool IsBlocking { get; }

        /// <summary>
        /// بررسی اینکه آیا دانلود باید رهگیری شود
        /// </summary>
        FilterResult ShouldIntercept(DownloadFilterContext context);
    }

    /// <summary>
    /// کنتکست فیلتر دانلود
    /// </summary>
    public class DownloadFilterContext
    {
        public string Url { get; set; } = "";
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Referrer { get; set; }
        public string Browser { get; set; } = "";
        public DownloadInterceptionSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// نتیجه فیلتر
    /// </summary>
    public class FilterResult
    {
        public bool ShouldIntercept { get; set; }
        public string Reason { get; set; } = "";
        public string FilterName { get; set; } = "";
    }

    /// <summary>
    /// فیلتر اندازه فایل
    /// </summary>
    public class FileSizeFilter : IDownloadFilter
    {
        public string Name => "File Size Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => true;

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = true, Reason = "Filter disabled", FilterName = Name };

            if (!context.FileSize.HasValue)
                return new FilterResult { ShouldIntercept = true, Reason = "File size unknown", FilterName = Name };

            var fileSize = context.FileSize.Value;

            // بررسی حداقل اندازه
            if (fileSize < context.Settings.MinFileSizeForInterception)
            {
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = $"File too small: {fileSize} bytes (min: {context.Settings.MinFileSizeForInterception})",
                    FilterName = Name
                };
            }

            // بررسی حداکثر اندازه
            if (context.Settings.MaxFileSizeForInterception > 0 && fileSize > context.Settings.MaxFileSizeForInterception)
            {
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = $"File too large: {fileSize} bytes (max: {context.Settings.MaxFileSizeForInterception})",
                    FilterName = Name
                };
            }

            return new FilterResult { ShouldIntercept = true, Reason = "File size acceptable", FilterName = Name };
        }
    }

    /// <summary>
    /// فیلتر نوع فایل
    /// </summary>
    public class FileTypeFilter : IDownloadFilter
    {
        public string Name => "File Type Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false; // غیرمسدودکننده - فقط تشویق می‌کند

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = false, Reason = "Filter disabled", FilterName = Name };

            if (string.IsNullOrEmpty(context.FileName))
                return new FilterResult { ShouldIntercept = context.Settings.InterceptUnknownFileTypes, Reason = "No filename", FilterName = Name };

            var extension = System.IO.Path.GetExtension(context.FileName)?.ToLower().TrimStart('.');
            if (string.IsNullOrEmpty(extension))
                return new FilterResult { ShouldIntercept = context.Settings.InterceptUnknownFileTypes, Reason = "No file extension", FilterName = Name };

            var shouldIntercept = context.Settings.InterceptableFileTypes.Contains(extension);
            return new FilterResult
            {
                ShouldIntercept = shouldIntercept,
                Reason = shouldIntercept ? $"File type '{extension}' is interceptable" : $"File type '{extension}' not in interceptable list",
                FilterName = Name
            };
        }
    }

    /// <summary>
    /// فیلتر دامنه
    /// </summary>
    public class DomainFilter : IDownloadFilter
    {
        public string Name => "Domain Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => true;

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = true, Reason = "Filter disabled", FilterName = Name };

            if (context.Settings.IsDomainExcluded(context.Url))
            {
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = "Domain is excluded",
                    FilterName = Name
                };
            }

            return new FilterResult { ShouldIntercept = true, Reason = "Domain not excluded", FilterName = Name };
        }
    }

    /// <summary>
    /// فیلتر مسدودسازی فایل‌های HTML
    /// </summary>
    public class HtmlFileFilter : IDownloadFilter
    {
        public string Name => "HTML File Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => true;

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = true, Reason = "Filter disabled", FilterName = Name };

            // استفاده از متد ShouldBlockHtmlDownload از Settings
            if (Models.Settings.ShouldBlockHtmlDownload(context.Url, context.FileName))
            {
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = "HTML files are blocked by settings",
                    FilterName = Name
                };
            }

            return new FilterResult { ShouldIntercept = true, Reason = "File is not HTML or HTML blocking is disabled", FilterName = Name };
        }
    }

    /// <summary>
    /// فیلتر الگوی URL
    /// </summary>
    public class UrlPatternFilter : IDownloadFilter
    {
        public string Name => "URL Pattern Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => true;

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = true, Reason = "Filter disabled", FilterName = Name };

            if (context.Settings.IsUrlPatternExcluded(context.Url))
            {
                return new FilterResult
                {
                    ShouldIntercept = false,
                    Reason = "URL matches excluded pattern",
                    FilterName = Name
                };
            }

            return new FilterResult { ShouldIntercept = true, Reason = "URL doesn't match excluded patterns", FilterName = Name };
        }
    }

    /// <summary>
    /// فیلتر MIME type
    /// </summary>
    public class MimeTypeFilter : IDownloadFilter
    {
        public string Name => "MIME Type Filter";
        public bool IsEnabled { get; set; } = true;
        public bool IsBlocking => false; // غیرمسدودکننده

        public FilterResult ShouldIntercept(DownloadFilterContext context)
        {
            if (!IsEnabled)
                return new FilterResult { ShouldIntercept = false, Reason = "Filter disabled", FilterName = Name };

            if (string.IsNullOrEmpty(context.MimeType))
                return new FilterResult { ShouldIntercept = context.Settings.InterceptUnknownFileTypes, Reason = "No MIME type", FilterName = Name };

            var shouldIntercept = context.Settings.InterceptableMimeTypes.Any(mime => 
                context.MimeType.StartsWith(mime, StringComparison.OrdinalIgnoreCase));

            return new FilterResult
            {
                ShouldIntercept = shouldIntercept,
                Reason = shouldIntercept ? $"MIME type '{context.MimeType}' is interceptable" : $"MIME type '{context.MimeType}' not interceptable",
                FilterName = Name
            };
        }
    }
}