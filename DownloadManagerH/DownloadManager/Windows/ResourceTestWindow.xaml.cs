using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DownloadManagerH.Models;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Windows
{
    public partial class ResourceTestWindow : Window
    {
        private readonly ILogger _logger;
        private bool _isRunning = false;

        public ResourceTestWindow()
        {
            InitializeComponent();
            _logger = LoggerFactory.GetDefaultLogger();
        }

        private async void RunTestsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _isRunning = true;
            RunTestsButton.IsEnabled = false;
            TestProgressBar.Visibility = Visibility.Visible;
            TestProgressBar.IsIndeterminate = true;

            TestResultsPanel.Children.Clear();
            AddTestMessage("🚀 شروع تست‌های عملکرد مدیریت منابع...", Brushes.Blue);

            try
            {
                var testRunner = new ResourceManagementTests(_logger);
                var testSuite = await testRunner.RunAllTestsAsync();

                // نمایش نتایج کلی
                AddTestMessage($"📊 نتایج کلی:", Brushes.Black, true);
                AddTestMessage($"   • تعداد کل تست‌ها: {testSuite.TotalCount}", Brushes.Black);
                AddTestMessage($"   • تست‌های موفق: {testSuite.PassedCount}", Brushes.Green);
                AddTestMessage($"   • تست‌های ناموفق: {testSuite.FailedCount}", testSuite.FailedCount > 0 ? Brushes.Red : Brushes.Black);
                AddTestMessage($"   • نرخ موفقیت: {testSuite.SuccessRate:F1}%", testSuite.SuccessRate >= 80 ? Brushes.Green : Brushes.Orange);
                AddTestMessage($"   • مدت زمان کل: {testSuite.Duration.TotalSeconds:F2} ثانیه", Brushes.Black);

                AddTestMessage("", Brushes.Black); // خط خالی

                // نمایش جزئیات هر تست
                AddTestMessage("📋 جزئیات تست‌ها:", Brushes.Black, true);
                foreach (var result in testSuite.Results)
                {
                    var statusIcon = result.Passed ? "✅" : "❌";
                    var statusColor = result.Passed ? Brushes.Green : Brushes.Red;
                    
                    AddTestMessage($"{statusIcon} {result.Name}", statusColor, true);
                    AddTestMessage($"   • مدت زمان: {result.Duration.TotalMilliseconds:F0}ms", Brushes.Gray);
                    
                    if (!string.IsNullOrEmpty(result.Details))
                    {
                        AddTestMessage($"   • جزئیات: {result.Details}", Brushes.Gray);
                    }
                    
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        AddTestMessage($"   • خطا: {result.Error}", Brushes.Red);
                    }

                    // نمایش معیارهای عملکرد
                    if (result.Metrics.Count > 0)
                    {
                        AddTestMessage($"   • معیارهای عملکرد:", Brushes.Gray);
                        foreach (var metric in result.Metrics)
                        {
                            AddTestMessage($"     - {metric.Key}: {metric.Value}", Brushes.LightGray);
                        }
                    }
                    
                    AddTestMessage("", Brushes.Black); // خط خالی
                }

                // نمایش پیام نهایی
                if (testSuite.FailedCount == 0)
                {
                    AddTestMessage("🎉 همه تست‌ها با موفقیت انجام شدند!", Brushes.Green, true);
                }
                else
                {
                    AddTestMessage($"⚠️ {testSuite.FailedCount} تست ناموفق بود. لطفاً لاگ‌ها را بررسی کنید.", Brushes.Orange, true);
                }
            }
            catch (Exception ex)
            {
                AddTestMessage($"❌ خطا در اجرای تست‌ها: {ex.Message}", Brushes.Red, true);
                _logger.LogError("خطا در اجرای تست‌های عملکرد", ex);
            }
            finally
            {
                _isRunning = false;
                RunTestsButton.IsEnabled = true;
                TestProgressBar.Visibility = Visibility.Collapsed;
                TestProgressBar.IsIndeterminate = false;
            }
        }

        private void AddTestMessage(string message, Brush color, bool isBold = false)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = color,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                Margin = new Thickness(0, 1, 0, 1)
            };

            if (isBold)
            {
                textBlock.FontWeight = FontWeights.Bold;
            }

            TestResultsPanel.Children.Add(textBlock);

            // Auto-scroll to bottom
            if (TestResultsPanel.Parent is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            TestResultsPanel.Children.Clear();
            AddTestMessage("تست‌ها پاک شدند. برای شروع مجدد، روی دکمه 'اجرای تست‌ها' کلیک کنید.", Brushes.Gray);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                var result = MessageBox.Show("تست‌ها در حال اجرا هستند. آیا مطمئن هستید که می‌خواهید پنجره را ببندید؟", 
                    "تأیید بستن", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // اطمینان از توقف تست‌ها در صورت بستن پنجره
            _isRunning = false;
            base.OnClosed(e);
        }
    }
}