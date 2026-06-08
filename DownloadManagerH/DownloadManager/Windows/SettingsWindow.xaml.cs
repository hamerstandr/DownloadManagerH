using System.Windows;
using DownloadManagerH.Models;
using DownloadManagerH.Windows;
using System.Diagnostics;
using System.IO;

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
            txtThemeColor.SelectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Settings.ThemeColor);
            toggleAddDirect.IsChecked = Settings.AddDownloadsDirectly;
            toggleAddDirect.Checked += (s, e) => Settings.AddDownloadsDirectly = true;
            toggleAddDirect.Unchecked += (s, e) => Settings.AddDownloadsDirectly = false;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {

            Settings.ParallelParts = txtParts.Value;
            Settings.MaxConcurrentDownloadsLimit = numMaxConcurrent.Value;
            Settings.CountConctionDownloads = numConcurrentDownloads.Value;
            Settings.EnableStartup = toggleStartup.IsChecked == true;
            Settings.MonitorClipboard = toggleClipboard.IsChecked == true;
            Settings.DefaultDownloadPath = txtDefaultPath.Text;
            Settings.ThemeColor = txtThemeColor.SelectedColor.ToString();
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
    }
} 