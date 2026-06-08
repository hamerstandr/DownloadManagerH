using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت تاریخچه دانلود با قابلیت صادرات و واردات
    /// </summary>
    public class HistoryManager
    {
        private readonly ILogger _logger;
        private readonly string _historyFilePath;
        private readonly List<DownloadHistoryItem> _history;

        public HistoryManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _historyFilePath = Path.Combine(Settings.DataDirectory, "download_history.json");
            _history = new List<DownloadHistoryItem>();
            
            // ایجاد دایرکتوری در صورت عدم وجود
            Directory.CreateDirectory(Settings.DataDirectory);
            
            // بارگذاری تاریخچه موجود
            LoadHistoryAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// افزودن آیتم جدید به تاریخچه
        /// </summary>
        public async Task AddToHistoryAsync(DownloadItem item)
        {
            try
            {
                var historyItem = new DownloadHistoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Url = item.Url,
                    FileName = item.FileName,
                    SavePath = item.SavePath,
                    Status = item.Status,
                    TotalBytes = item.TotalBytes,
                    DownloadedBytes = item.DownloadedBytes,
                    StartTime = DateTime.Now,
                    CompletionTime = item.Status == DownloadStatus.Completed ? DateTime.Now : null,
                    Group = item.Group,
                    Priority = item.Priority,
                    RetryCount = item.RetryCount,
                    Statistics = item.Statistics
                };

                _history.Add(historyItem);
                await SaveHistoryAsync();
                
                _logger.LogInfo($"آیتم جدید به تاریخچه اضافه شد: {item.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در افزودن آیتم به تاریخچه", ex);
            }
        }

        /// <summary>
        /// دریافت تاریخچه کامل
        /// </summary>
        public IReadOnlyList<DownloadHistoryItem> GetHistory()
        {
            return _history.AsReadOnly();
        }

        /// <summary>
        /// دریافت تاریخچه با فیلتر
        /// </summary>
        public IEnumerable<DownloadHistoryItem> GetHistoryFiltered(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            DownloadStatus? status = null,
            string group = null)
        {
            var query = _history.AsEnumerable();

            if (fromDate.HasValue)
                query = query.Where(h => h.StartTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(h => h.StartTime <= toDate.Value);

            if (status.HasValue)
                query = query.Where(h => h.Status == status.Value);

            if (!string.IsNullOrEmpty(group))
                query = query.Where(h => h.Group == group);

            return query.OrderByDescending(h => h.StartTime);
        }

        /// <summary>
        /// صادرات تاریخچه به فرمت JSON
        /// </summary>
        public async Task<bool> ExportToJsonAsync(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(_history, options);
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInfo($"تاریخچه به فرمت JSON صادر شد: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در صادرات JSON: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// صادرات تاریخچه به فرمت CSV
        /// </summary>
        public async Task<bool> ExportToCsvAsync(string filePath)
        {
            try
            {
                var csv = new List<string>
                {
                    "ID,URL,FileName,SavePath,Status,TotalBytes,DownloadedBytes,StartTime,CompletionTime,Group,Priority,RetryCount"
                };

                foreach (var item in _history)
                {
                    csv.Add($"\"{item.Id}\",\"{item.Url}\",\"{item.FileName}\",\"{item.SavePath}\"," +
                           $"\"{item.Status}\",{item.TotalBytes},{item.DownloadedBytes}," +
                           $"\"{item.StartTime:yyyy-MM-dd HH:mm:ss}\",\"{item.CompletionTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}\"," +
                           $"\"{item.Group}\",\"{item.Priority}\",{item.RetryCount}");
                }

                await File.WriteAllLinesAsync(filePath, csv);
                
                _logger.LogInfo($"تاریخچه به فرمت CSV صادر شد: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در صادرات CSV: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// صادرات تاریخچه به فرمت XML
        /// </summary>
        public async Task<bool> ExportToXmlAsync(string filePath)
        {
            try
            {
                var root = new XElement("DownloadHistory");

                foreach (var item in _history)
                {
                    var element = new XElement("DownloadItem",
                        new XElement("Id", item.Id),
                        new XElement("Url", item.Url),
                        new XElement("FileName", item.FileName),
                        new XElement("SavePath", item.SavePath),
                        new XElement("Status", item.Status),
                        new XElement("TotalBytes", item.TotalBytes),
                        new XElement("DownloadedBytes", item.DownloadedBytes),
                        new XElement("StartTime", item.StartTime.ToString("yyyy-MM-dd HH:mm:ss")),
                        new XElement("CompletionTime", item.CompletionTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                        new XElement("Group", item.Group ?? ""),
                        new XElement("Priority", item.Priority),
                        new XElement("RetryCount", item.RetryCount)
                    );
                    root.Add(element);
                }

                var doc = new XDocument(root);
                await Task.Run(() => doc.Save(filePath));
                
                _logger.LogInfo($"تاریخچه به فرمت XML صادر شد: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در صادرات XML: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// واردات تاریخچه از فایل JSON
        /// </summary>
        public async Task<bool> ImportFromJsonAsync(string filePath, bool mergeWithExisting = true)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning($"فایل JSON یافت نشد: {filePath}");
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var importedHistory = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json);

                if (importedHistory == null || !importedHistory.Any())
                {
                    _logger.LogWarning("فایل JSON خالی یا نامعتبر است");
                    return false;
                }

                if (!mergeWithExisting)
                {
                    _history.Clear();
                }

                // اضافه کردن آیتم‌های جدید (جلوگیری از تکرار بر اساس URL و زمان شروع)
                var existingItems = _history.Select(h => $"{h.Url}_{h.StartTime:yyyyMMddHHmmss}").ToHashSet();
                
                foreach (var item in importedHistory)
                {
                    var key = $"{item.Url}_{item.StartTime:yyyyMMddHHmmss}";
                    if (!existingItems.Contains(key))
                    {
                        _history.Add(item);
                    }
                }

                await SaveHistoryAsync();
                
                _logger.LogInfo($"تاریخچه از JSON وارد شد: {importedHistory.Count} آیتم");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در واردات JSON: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// پاک‌سازی تاریخچه بر اساس تاریخ
        /// </summary>
        public async Task<int> CleanupHistoryAsync(DateTime olderThan)
        {
            try
            {
                var itemsToRemove = _history.Where(h => h.StartTime < olderThan).ToList();
                var removedCount = itemsToRemove.Count;

                foreach (var item in itemsToRemove)
                {
                    _history.Remove(item);
                }

                if (removedCount > 0)
                {
                    await SaveHistoryAsync();
                    _logger.LogInfo($"پاک‌سازی تاریخچه: {removedCount} آیتم حذف شد");
                }

                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پاک‌سازی تاریخچه", ex);
                return 0;
            }
        }

        /// <summary>
        /// پاک‌سازی تاریخچه بر اساس وضعیت
        /// </summary>
        public async Task<int> CleanupHistoryByStatusAsync(DownloadStatus status)
        {
            try
            {
                var itemsToRemove = _history.Where(h => h.Status == status).ToList();
                var removedCount = itemsToRemove.Count;

                foreach (var item in itemsToRemove)
                {
                    _history.Remove(item);
                }

                if (removedCount > 0)
                {
                    await SaveHistoryAsync();
                    _logger.LogInfo($"پاک‌سازی تاریخچه بر اساس وضعیت {status}: {removedCount} آیتم حذف شد");
                }

                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در پاک‌سازی تاریخچه بر اساس وضعیت {status}", ex);
                return 0;
            }
        }

        /// <summary>
        /// دریافت آمار تاریخچه
        /// </summary>
        public HistoryStatistics GetStatistics()
        {
            var stats = new HistoryStatistics
            {
                TotalDownloads = _history.Count,
                CompletedDownloads = _history.Count(h => h.Status == DownloadStatus.Completed),
                FailedDownloads = _history.Count(h => h.Status == DownloadStatus.Failed),
                TotalBytesDownloaded = _history.Where(h => h.Status == DownloadStatus.Completed)
                                              .Sum(h => h.TotalBytes),
                AverageDownloadSize = _history.Where(h => h.Status == DownloadStatus.Completed && h.TotalBytes > 0)
                                             .DefaultIfEmpty()
                                             .Average(h => h?.TotalBytes ?? 0),
                MostActiveDay = _history.GroupBy(h => h.StartTime.Date)
                                       .OrderByDescending(g => g.Count())
                                       .FirstOrDefault()?.Key,
                TopGroups = _history.Where(h => !string.IsNullOrEmpty(h.Group))
                                   .GroupBy(h => h.Group)
                                   .OrderByDescending(g => g.Count())
                                   .Take(5)
                                   .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }

        /// <summary>
        /// بارگذاری تاریخچه از فایل
        /// </summary>
        private async Task LoadHistoryAsync()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = await File.ReadAllTextAsync(_historyFilePath);
                    var history = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json);
                    
                    if (history != null)
                    {
                        _history.Clear();
                        _history.AddRange(history);
                        _logger.LogInfo($"تاریخچه بارگذاری شد: {_history.Count} آیتم");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بارگذاری تاریخچه", ex);
            }
        }

        /// <summary>
        /// ذخیره تاریخچه در فایل
        /// </summary>
        private async Task SaveHistoryAsync()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(_history, options);
                await File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در ذخیره تاریخچه", ex);
            }
        }
    }

    /// <summary>
    /// آیتم تاریخچه دانلود
    /// </summary>
    public class DownloadHistoryItem
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string SavePath { get; set; }
        public DownloadStatus Status { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string Group { get; set; }
        public DownloadPriority Priority { get; set; }
        public int RetryCount { get; set; }
        public DownloadStatistics Statistics { get; set; }

        /// <summary>
        /// مدت زمان دانلود
        /// </summary>
        public TimeSpan? Duration => CompletionTime.HasValue ? CompletionTime.Value - StartTime : null;

        /// <summary>
        /// درصد تکمیل
        /// </summary>
        public double CompletionPercentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
    }

    /// <summary>
    /// آمار تاریخچه دانلود
    /// </summary>
    public class HistoryStatistics
    {
        public int TotalDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public double AverageDownloadSize { get; set; }
        public DateTime? MostActiveDay { get; set; }
        public Dictionary<string, int> TopGroups { get; set; } = new();

        /// <summary>
        /// نرخ موفقیت دانلود
        /// </summary>
        public double SuccessRate => TotalDownloads > 0 ? (double)CompletedDownloads / TotalDownloads * 100 : 0;

        /// <summary>
        /// اندازه کل دانلود شده به صورت قابل خواندن
        /// </summary>
        public string TotalBytesDownloadedFormatted => FormatFileSize(TotalBytesDownloaded);

        /// <summary>
        /// میانگین اندازه دانلود به صورت قابل خواندن
        /// </summary>
        public string AverageDownloadSizeFormatted => FormatFileSize((long)AverageDownloadSize);

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:F2} {sizes[order]}";
        }
    }
}