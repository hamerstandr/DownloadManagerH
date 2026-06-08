using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Windows
{
    /// <summary>
    /// پنجره نمایش لاگ‌ها
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        private readonly ILogger _logger;
        private readonly MemoryLogger _memoryLogger;
        private readonly ObservableCollection<LogEntry> _logEntries;
        private readonly CollectionViewSource _logViewSource;
        private bool _autoScroll = true;
        private bool _isLoading = false;

        /// <summary>
        /// سازنده پنجره نمایش لاگ
        /// </summary>
        /// <param name="logger">لاگر اصلی</param>
        /// <param name="memoryLogger">لاگر حافظه‌ای برای نمایش زنده</param>
        public LogViewerWindow(ILogger logger = null, MemoryLogger memoryLogger = null)
        {
            InitializeComponent();
            
            _logger = logger ?? LoggerFactory.GetDefaultLogger();
            _memoryLogger = memoryLogger;
            _logEntries = new ObservableCollection<LogEntry>();
            
            // تنظیم CollectionViewSource برای فیلتر و مرتب‌سازی
            _logViewSource = new CollectionViewSource { Source = _logEntries };
            _logViewSource.Filter += LogViewSource_Filter;
            _logViewSource.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
            
            lvLogs.ItemsSource = _logViewSource.View;
            
            // اتصال به رویدادهای لاگر حافظه‌ای
            if (_memoryLogger != null)
            {
                _memoryLogger.LogAdded += MemoryLogger_LogAdded;
            }
            
            // بارگذاری اولیه لاگ‌ها
            _ = LoadLogsAsync();
            
            // تنظیم تاریخ‌های پیش‌فرض
            dpFromDate.SelectedDate = DateTime.Today.AddDays(-7); // یک هفته گذشته
            dpToDate.SelectedDate = DateTime.Today.AddDays(1); // فردا
            
            UpdateStatusBar();
        }

        #region Event Handlers

        /// <summary>
        /// رویداد اضافه شدن لاگ جدید به حافظه
        /// </summary>
        private void MemoryLogger_LogAdded(object sender, LogEntry e)
        {
            Dispatcher.Invoke(() =>
            {
                _logEntries.Insert(0, e); // اضافه کردن به ابتدای لیست
                
                // حذف لاگ‌های اضافی برای جلوگیری از مصرف زیاد حافظه
                while (_logEntries.Count > 1000)
                {
                    _logEntries.RemoveAt(_logEntries.Count - 1);
                }
                
                UpdateLogCount();
                
                // پیمایش خودکار به آخرین لاگ
                if (_autoScroll && lvLogs.Items.Count > 0)
                {
                    lvLogs.ScrollIntoView(lvLogs.Items[0]);
                }
            });
        }

        /// <summary>
        /// فیلتر سطح لاگ
        /// </summary>
        private void LogLevelFilter_Click(object sender, RoutedEventArgs e)
        {
            RefreshFilter();
        }

        /// <summary>
        /// تغییر متن جستجو
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshFilter();
        }

        /// <summary>
        /// پاک کردن جستجو
        /// </summary>
        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
        }

        /// <summary>
        /// تغییر فیلتر تاریخ
        /// </summary>
        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            RefreshFilter();
        }

        /// <summary>
        /// بروزرسانی لاگ‌ها
        /// </summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        /// <summary>
        /// پاک کردن لاگ‌های نمایش داده شده
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا مطمئن هستید که می‌خواهید همه لاگ‌های نمایش داده شده را پاک کنید؟",
                "تأیید پاک کردن",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _logEntries.Clear();
                _memoryLogger?.Clear();
                UpdateLogCount();
            }
        }

        /// <summary>
        /// صادرات لاگ‌ها
        /// </summary>
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json",
                DefaultExt = "txt",
                FileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    btnExport.IsEnabled = false;
                    btnExport.Content = "در حال صادرات...";

                    var filteredLogs = _logViewSource.View.Cast<LogEntry>().ToList();
                    await ExportLogsAsync(filteredLogs, saveDialog.FileName);

                    MessageBox.Show($"لاگ‌ها با موفقیت صادر شدند:\n{saveDialog.FileName}", 
                        "صادرات موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در صادرات لاگ‌ها:\n{ex.Message}", 
                        "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnExport.IsEnabled = true;
                    btnExport.Content = "📤 صادرات";
                }
            }
        }

        /// <summary>
        /// تغییر وضعیت پیمایش خودکار
        /// </summary>
        private void BtnAutoScroll_Click(object sender, RoutedEventArgs e)
        {
            _autoScroll = !_autoScroll;
            UpdateAutoScrollButton();
        }

        /// <summary>
        /// دوبار کلیک روی لاگ برای نمایش جزئیات
        /// </summary>
        private void LvLogs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lvLogs.SelectedItem is LogEntry selectedLog)
            {
                ShowLogDetails(selectedLog);
            }
        }

        /// <summary>
        /// فیلتر لاگ‌ها
        /// </summary>
        private void LogViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is LogEntry logEntry)
            {
                e.Accepted = ShouldShowLogEntry(logEntry);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// بارگذاری لاگ‌ها از منابع مختلف
        /// </summary>
        private async Task LoadLogsAsync()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                btnRefresh.IsEnabled = false;
                btnRefresh.Content = "در حال بارگذاری...";

                _logEntries.Clear();

                // بارگذاری از لاگر حافظه‌ای
                if (_memoryLogger != null)
                {
                    var memoryLogs = await _memoryLogger.GetLogsAsync(LogLevel.Debug);
                    foreach (var log in memoryLogs.OrderByDescending(l => l.Timestamp))
                    {
                        _logEntries.Add(log);
                    }
                }

                // بارگذاری از لاگر اصلی (فایل)
                if (_logger != null && _logger != _memoryLogger)
                {
                    try
                    {
                        var fromDate = dpFromDate.SelectedDate;
                        var toDate = dpToDate.SelectedDate;
                        
                        var fileLogs = await _logger.GetLogsAsync(LogLevel.Debug, fromDate, toDate);
                        
                        // ترکیب لاگ‌ها و حذف تکراری‌ها
                        var existingTimestamps = new HashSet<DateTime>(_logEntries.Select(l => l.Timestamp));
                        
                        foreach (var log in fileLogs.Where(l => !existingTimestamps.Contains(l.Timestamp)))
                        {
                            _logEntries.Add(log);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("خطا در بارگذاری لاگ‌ها از فایل", ex);
                    }
                }

                // مرتب‌سازی نهایی
                var sortedLogs = _logEntries.OrderByDescending(l => l.Timestamp).ToList();
                _logEntries.Clear();
                foreach (var log in sortedLogs)
                {
                    _logEntries.Add(log);
                }

                UpdateLogCount();
                UpdateStatusBar();
            }
            finally
            {
                _isLoading = false;
                btnRefresh.IsEnabled = true;
                btnRefresh.Content = "🔄 بروزرسانی";
            }
        }

        /// <summary>
        /// بروزرسانی فیلتر
        /// </summary>
        private void RefreshFilter()
        {
            _logViewSource.View.Refresh();
            UpdateLogCount();
            UpdateFilterStatus();
        }

        /// <summary>
        /// تعیین اینکه آیا لاگ باید نمایش داده شود
        /// </summary>
        private bool ShouldShowLogEntry(LogEntry logEntry)
        {
            // فیلتر سطح لاگ
            var showDebug = btnDebug.IsChecked == true;
            var showInfo = btnInfo.IsChecked == true;
            var showWarning = btnWarning.IsChecked == true;
            var showError = btnError.IsChecked == true;

            var levelMatch = logEntry.Level switch
            {
                LogLevel.Debug => showDebug,
                LogLevel.Info => showInfo,
                LogLevel.Warning => showWarning,
                LogLevel.Error => showError,
                _ => true
            };

            if (!levelMatch) return false;

            // فیلتر جستجو
            var searchText = txtSearch.Text?.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                var searchMatch = logEntry.Message?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true ||
                                logEntry.Category?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true ||
                                logEntry.Context?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
                
                if (!searchMatch) return false;
            }

            // فیلتر تاریخ
            if (dpFromDate.SelectedDate.HasValue && logEntry.Timestamp.Date < dpFromDate.SelectedDate.Value.Date)
                return false;

            if (dpToDate.SelectedDate.HasValue && logEntry.Timestamp.Date > dpToDate.SelectedDate.Value.Date)
                return false;

            return true;
        }

        /// <summary>
        /// صادرات لاگ‌ها به فایل
        /// </summary>
        private async Task ExportLogsAsync(List<LogEntry> logs, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".txt":
                    await ExportToTextAsync(logs, filePath);
                    break;
                case ".csv":
                    await ExportToCsvAsync(logs, filePath);
                    break;
                case ".json":
                    await ExportToJsonAsync(logs, filePath);
                    break;
                default:
                    throw new NotSupportedException($"فرمت فایل پشتیبانی نمی‌شود: {extension}");
            }
        }

        /// <summary>
        /// صادرات به فایل متنی
        /// </summary>
        private async Task ExportToTextAsync(List<LogEntry> logs, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            
            await writer.WriteLineAsync($"# گزارش لاگ‌ها - {DateTime.Now}");
            await writer.WriteLineAsync($"# تعداد کل: {logs.Count}");
            await writer.WriteLineAsync();

            foreach (var log in logs)
            {
                await writer.WriteLineAsync(log.ToString());
                
                if (log.Exception != null)
                {
                    await writer.WriteLineAsync($"  Stack Trace: {log.Exception.StackTrace}");
                }
                
                await writer.WriteLineAsync();
            }
        }

        /// <summary>
        /// صادرات به فایل CSV
        /// </summary>
        private async Task ExportToCsvAsync(List<LogEntry> logs, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            
            // هدر
            await writer.WriteLineAsync("Timestamp,Level,Category,Message,Context,Exception");

            foreach (var log in logs)
            {
                var line = $"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\"," +
                          $"\"{log.Level}\"," +
                          $"\"{log.Category}\"," +
                          $"\"{log.Message?.Replace("\"", "\"\"")}\"," +
                          $"\"{log.Context?.Replace("\"", "\"\"")}\"," +
                          $"\"{log.Exception?.Message?.Replace("\"", "\"\"")}\"";
                
                await writer.WriteLineAsync(line);
            }
        }

        /// <summary>
        /// صادرات به فایل JSON
        /// </summary>
        private async Task ExportToJsonAsync(List<LogEntry> logs, string filePath)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(logs, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// نمایش جزئیات لاگ
        /// </summary>
        private void ShowLogDetails(LogEntry logEntry)
        {
            var details = $"زمان: {logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\n" +
                         $"سطح: {logEntry.Level}\n" +
                         $"دسته‌بندی: {logEntry.Category}\n" +
                         $"پیام: {logEntry.Message}\n";

            if (!string.IsNullOrEmpty(logEntry.Context))
            {
                details += $"زمینه: {logEntry.Context}\n";
            }

            if (logEntry.Properties?.Any() == true)
            {
                details += "خصوصیات:\n";
                foreach (var prop in logEntry.Properties)
                {
                    details += $"  {prop.Key}: {prop.Value}\n";
                }
            }

            if (logEntry.Exception != null)
            {
                details += $"\nاستثنا: {logEntry.Exception.Message}\n";
                details += $"Stack Trace:\n{logEntry.Exception.StackTrace}";
            }

            MessageBox.Show(details, "جزئیات لاگ", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// بروزرسانی تعداد لاگ‌ها
        /// </summary>
        private void UpdateLogCount()
        {
            var totalCount = _logEntries.Count;
            var filteredCount = _logViewSource.View.Cast<LogEntry>().Count();
            
            lblLogCount.Text = filteredCount == totalCount ? 
                totalCount.ToString() : 
                $"{filteredCount} از {totalCount}";
        }

        /// <summary>
        /// بروزرسانی نوار وضعیت
        /// </summary>
        private void UpdateStatusBar()
        {
            lblLastUpdate.Text = DateTime.Now.ToString("HH:mm:ss");
            UpdateAutoScrollButton();
            UpdateFilterStatus();
        }

        /// <summary>
        /// بروزرسانی دکمه پیمایش خودکار
        /// </summary>
        private void UpdateAutoScrollButton()
        {
            btnAutoScroll.Content = _autoScroll ? "📜 پیمایش خودکار: فعال" : "📜 پیمایش خودکار: غیرفعال";
            lblAutoScrollStatus.Text = _autoScroll ? "پیمایش خودکار: فعال" : "پیمایش خودکار: غیرفعال";
        }

        /// <summary>
        /// بروزرسانی وضعیت فیلتر
        /// </summary>
        private void UpdateFilterStatus()
        {
            var activeFilters = new List<string>();

            if (!(btnDebug.IsChecked == true && btnInfo.IsChecked == true && 
                  btnWarning.IsChecked == true && btnError.IsChecked == true))
            {
                var levels = new List<string>();
                if (btnDebug.IsChecked == true) levels.Add("Debug");
                if (btnInfo.IsChecked == true) levels.Add("Info");
                if (btnWarning.IsChecked == true) levels.Add("Warning");
                if (btnError.IsChecked == true) levels.Add("Error");
                
                if (levels.Any())
                    activeFilters.Add($"سطح: {string.Join(", ", levels)}");
            }

            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                activeFilters.Add($"جستجو: {txtSearch.Text}");
            }

            if (dpFromDate.SelectedDate.HasValue || dpToDate.SelectedDate.HasValue)
            {
                activeFilters.Add("تاریخ");
            }

            lblFilterStatus.Text = activeFilters.Any() ? 
                $"فیلتر: {string.Join(", ", activeFilters)}" : 
                "فیلتر: همه";
        }

        #endregion

        /// <summary>
        /// بستن پنجره
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // قطع اتصال از رویدادها
            if (_memoryLogger != null)
            {
                _memoryLogger.LogAdded -= MemoryLogger_LogAdded;
            }

            base.OnClosing(e);
        }
    }
}