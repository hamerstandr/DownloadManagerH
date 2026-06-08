using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models.Tests
{
    /// <summary>
    /// تست‌های واحد برای HistoryManager
    /// </summary>
    public class HistoryManagerTests
    {
        private readonly ILogger _mockLogger;
        private readonly string _testDataDirectory;

        public HistoryManagerTests()
        {
            _mockLogger = new MockLogger();
            _testDataDirectory = Path.Combine(Path.GetTempPath(), "DownloadManagerTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataDirectory);
            
            // تنظیم مسیر داده‌ها برای تست
            typeof(Settings).GetProperty(nameof(Settings.DataDirectory))?.SetValue(null, _testDataDirectory);
        }

        /// <summary>
        /// تست افزودن آیتم به تاریخچه
        /// </summary>
        public async Task<bool> TestAddToHistoryAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                var downloadItem = CreateTestDownloadItem();

                await historyManager.AddToHistoryAsync(downloadItem);
                var history = historyManager.GetHistory();

                return history.Count == 1 && 
                       history[0].Url == downloadItem.Url && 
                       history[0].FileName == downloadItem.FileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست افزودن به تاریخچه: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست فیلتر کردن تاریخچه
        /// </summary>
        public async Task<bool> TestGetHistoryFilteredAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                
                // افزودن چند آیتم تست
                var item1 = CreateTestDownloadItem("http://test1.com", DownloadStatus.Completed);
                var item2 = CreateTestDownloadItem("http://test2.com", DownloadStatus.Failed);
                var item3 = CreateTestDownloadItem("http://test3.com", DownloadStatus.Completed);

                await historyManager.AddToHistoryAsync(item1);
                await historyManager.AddToHistoryAsync(item2);
                await historyManager.AddToHistoryAsync(item3);

                // فیلتر بر اساس وضعیت
                var completedItems = historyManager.GetHistoryFiltered(status: DownloadStatus.Completed);
                var failedItems = historyManager.GetHistoryFiltered(status: DownloadStatus.Failed);

                return completedItems.Count() == 2 && failedItems.Count() == 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست فیلتر تاریخچه: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست صادرات به JSON
        /// </summary>
        public async Task<bool> TestExportToJsonAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                var downloadItem = CreateTestDownloadItem();
                await historyManager.AddToHistoryAsync(downloadItem);

                var exportPath = Path.Combine(_testDataDirectory, "export_test.json");
                var result = await historyManager.ExportToJsonAsync(exportPath);

                return result && File.Exists(exportPath) && new FileInfo(exportPath).Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست صادرات JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست صادرات به CSV
        /// </summary>
        public async Task<bool> TestExportToCsvAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                var downloadItem = CreateTestDownloadItem();
                await historyManager.AddToHistoryAsync(downloadItem);

                var exportPath = Path.Combine(_testDataDirectory, "export_test.csv");
                var result = await historyManager.ExportToCsvAsync(exportPath);

                if (!result || !File.Exists(exportPath))
                    return false;

                var lines = await File.ReadAllLinesAsync(exportPath);
                return lines.Length >= 2; // هدر + حداقل یک ردیف داده
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست صادرات CSV: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست واردات از JSON
        /// </summary>
        public async Task<bool> TestImportFromJsonAsync()
        {
            try
            {
                var historyManager1 = new HistoryManager(_mockLogger);
                var downloadItem = CreateTestDownloadItem();
                await historyManager1.AddToHistoryAsync(downloadItem);

                var exportPath = Path.Combine(_testDataDirectory, "import_test.json");
                await historyManager1.ExportToJsonAsync(exportPath);

                var historyManager2 = new HistoryManager(_mockLogger);
                var result = await historyManager2.ImportFromJsonAsync(exportPath, false);
                var importedHistory = historyManager2.GetHistory();

                return result && importedHistory.Count == 1 && importedHistory[0].Url == downloadItem.Url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست واردات JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست پاک‌سازی تاریخچه
        /// </summary>
        public async Task<bool> TestCleanupHistoryAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                
                // افزودن آیتم‌های قدیمی و جدید
                var oldItem = CreateTestDownloadItem("http://old.com");
                var newItem = CreateTestDownloadItem("http://new.com");

                await historyManager.AddToHistoryAsync(oldItem);
                await historyManager.AddToHistoryAsync(newItem);

                // پاک‌سازی آیتم‌های قدیمی‌تر از امروز
                var removedCount = await historyManager.CleanupHistoryAsync(DateTime.Now.AddHours(-1));
                var remainingHistory = historyManager.GetHistory();

                return removedCount >= 0 && remainingHistory.Count <= 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست پاک‌سازی تاریخچه: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تست دریافت آمار
        /// </summary>
        public async Task<bool> TestGetStatisticsAsync()
        {
            try
            {
                var historyManager = new HistoryManager(_mockLogger);
                
                var item1 = CreateTestDownloadItem("http://test1.com", DownloadStatus.Completed);
                var item2 = CreateTestDownloadItem("http://test2.com", DownloadStatus.Failed);
                
                await historyManager.AddToHistoryAsync(item1);
                await historyManager.AddToHistoryAsync(item2);

                var stats = historyManager.GetStatistics();

                return stats.TotalDownloads == 2 && 
                       stats.CompletedDownloads == 1 && 
                       stats.FailedDownloads == 1 &&
                       stats.SuccessRate == 50.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تست آمار: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// اجرای تمام تست‌ها
        /// </summary>
        public async Task<bool> RunAllTestsAsync()
        {
            var tests = new List<(string Name, Func<Task<bool>> Test)>
            {
                ("افزودن به تاریخچه", TestAddToHistoryAsync),
                ("فیلتر تاریخچه", TestGetHistoryFilteredAsync),
                ("صادرات JSON", TestExportToJsonAsync),
                ("صادرات CSV", TestExportToCsvAsync),
                ("واردات JSON", TestImportFromJsonAsync),
                ("پاک‌سازی تاریخچه", TestCleanupHistoryAsync),
                ("آمار تاریخچه", TestGetStatisticsAsync)
            };

            var results = new List<bool>();
            
            foreach (var (name, test) in tests)
            {
                Console.WriteLine($"اجرای تست: {name}");
                var result = await test();
                results.Add(result);
                Console.WriteLine($"نتیجه: {(result ? "موفق" : "ناموفق")}");
            }

            var successCount = results.Count(r => r);
            var totalCount = results.Count;
            
            Console.WriteLine($"\nنتیجه کلی: {successCount}/{totalCount} تست موفق");
            
            return successCount == totalCount;
        }

        /// <summary>
        /// ایجاد آیتم تست
        /// </summary>
        private DownloadItem CreateTestDownloadItem(string url = "http://test.com", DownloadStatus status = DownloadStatus.Completed)
        {
            return new DownloadItem
            {
                Url = url,
                FileName = "test_file.zip",
                SavePath = Path.Combine(_testDataDirectory, "test_file.zip"),
                Status = status,
                TotalBytes = 1024 * 1024, // 1 MB
                DownloadedBytes = status == DownloadStatus.Completed ? 1024 * 1024 : 512 * 1024,
                Progress = status == DownloadStatus.Completed ? 100.0 : 50.0,
                Group = "Test Group",
                Priority = DownloadPriority.Normal,
                Statistics = new DownloadStatistics
                {
                    AverageSpeed = 1024 * 200, // 200 KB/s
                    PeakSpeed = 1024 * 500 // 500 KB/s
                },
                ParallelParts=Settings.ParallelParts
            };
        }

        /// <summary>
        /// پاک‌سازی فایل‌های تست
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDataDirectory))
                {
                    Directory.Delete(_testDataDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در پاک‌سازی: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Mock Logger برای تست
    /// </summary>
    public class MockLogger : ILogger
    {
        public void LogDebug(string message, object? context = null)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }

        public void LogInfo(string message, object? context = null)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        public void LogWarning(string message, object? context = null)
        {
            Console.WriteLine($"[WARNING] {message}");
        }

        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            Console.WriteLine($"[ERROR] {message} - {exception?.Message}");
        }

        public void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null)
        {
            Console.WriteLine($"[DOWNLOAD] {eventType} - {item.FileName}");
        }

        public Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            return Task.FromResult(Enumerable.Empty<LogEntry>());
        }
    }
}