using System.Windows;
using DownloadManagerH.Models;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using DownloadManagerH.Models.Logging;
using DownloadManagerH.Controls;

namespace DownloadManagerH.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            txtParts.Value = Settings.ParallelParts;
            numMaxConcurrent.Value = Settings.MaxConcurrentDownloadsLimit;
            numConcurrentDownloads.Value = Settings.CountConctionDownloads;
            StartupManager.SetStartup(Settings.EnableStartup);
            toggleStartup.IsChecked = Settings.EnableStartup;
            toggleClipboard.IsChecked = Settings.MonitorClipboard;
            toggleClipboard.Checked += (s, e) =>
            {
                Settings.MonitorClipboard = true;
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.EnableClipboardMonitor(true);
                }
            };
            toggleClipboard.Unchecked += (s, e) =>
            {
                Settings.MonitorClipboard = false;
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.EnableClipboardMonitor(false);
                }
            };
            txtDefaultPath.Text = Settings.DefaultDownloadPath;
            
            // بارگذاری رنگ تم
            string themeColor = Settings.ThemeColor;
            if (!string.IsNullOrEmpty(themeColor))
            {
                colorPickerTheme.SetHexColor(themeColor);
            }
            
            toggleAddDirect.IsChecked = Settings.AddDownloadsDirectly;
            toggleAddDirect.Checked += (s, e) => Settings.AddDownloadsDirectly = true;
            toggleAddDirect.Unchecked += (s, e) => Settings.AddDownloadsDirectly = false;
            
            // بارگذاری تنظیمات زبان
            LoadLanguageSettings();
        }

        /// <summary>
        /// بارگذاری تنظیمات زبان
        /// </summary>
        private void LoadLanguageSettings()
        {
            // انتخاب زبان فعلی در ComboBox
            string currentLanguage = Settings.Language.ToLower();
            foreach (var item in cmbLanguage.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Tag+"".ToLower() == currentLanguage)
                {
                    cmbLanguage.SelectedItem = comboItem;
                    break;
                }
            }
            
            // اگر هیچ زبانی انتخاب نشده بود، پیش‌فرض فارسی باشد
            if (cmbLanguage.SelectedItem == null)
            {
                foreach (var item in cmbLanguage.Items)
                {
                    if (item is ComboBoxItem comboItem && comboItem.Tag?.ToString() == "fa")
                    {
                        cmbLanguage.SelectedItem = comboItem;
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// رویداد تغییر زبان
        /// </summary>
        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbLanguage.SelectedItem is ComboBoxItem selectedLanguage)
            {
                string languageCode = selectedLanguage.Tag?.ToString() ?? "fa";
                Settings.Language = languageCode;
                
                // اعمال جهت متن بر اساس زبان
                this.FlowDirection = Settings.GetFlowDirection();
                
                // ذخیره فرهنگ برای کل برنامه
                var culture = Settings.GetCultureInfo();
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {

            Settings.ParallelParts = txtParts.Value;
            Settings.MaxConcurrentDownloadsLimit = numMaxConcurrent.Value;
            Settings.CountConctionDownloads = numConcurrentDownloads.Value;
            Settings.EnableStartup = toggleStartup.IsChecked == true;
            Settings.MonitorClipboard = toggleClipboard.IsChecked == true;
            Settings.DefaultDownloadPath = txtDefaultPath.Text;
            Settings.ThemeColor = colorPickerTheme.GetHexColor();
            DialogResult = true;
            StartupManager.SetStartup(Settings.EnableStartup);
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnDownloadChromeExtension_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions", "Chrome");
            Process.Start("explorer.exe", path);
        }
        private void BtnDownloadFirefoxExtension_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions", "Firefox");
            Process.Start("explorer.exe", path);
        }
        private void BtnDownloadEdgeExtension_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions", "Edge");
            Process.Start("explorer.exe", path);
        }

        /// <summary>
        /// بررسی و نصب Native Messaging برای مرورگرها
        /// </summary>
        private async void BtnCheckNativeMessaging_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logger = LoggerFactory.GetDefaultLogger();
                var registrar = new NativeMessagingRegistrar(logger);
                
                // نمایش وضعیت فعلی
                if (registrar.IsRegistered)
                {
                    CustomMessageBox.Show("Native Messaging قبلاً نصب شده است.", "وضعیت", CustomMessageBoxType.OK);
                }
                else
                {
                    var result = CustomMessageBox.Show("Native Messaging نصب نیست. آیا می‌خواهید آن را نصب کنید؟", "نصب", CustomMessageBoxType.YesNo);
                    if (result == CustomMessageBoxResult.Yes)
                    {
                        await registrar.RegisterHostAsync();
                        CustomMessageBox.Show("Native Messaging با موفقیت نصب شد.", "موفق", CustomMessageBoxType.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"خطا در بررسی/نصب Native Messaging: {ex.Message}", "خطا", CustomMessageBoxType.OK);
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // بارگذاری تنظیمات TrafficWatch
            chkTrafficWatch.IsChecked = Settings.EnableTrafficWatchIntegration;
            txtTrafficWatchPort.Text = Settings.TrafficWatchPort.ToString();
        }
    }
} 