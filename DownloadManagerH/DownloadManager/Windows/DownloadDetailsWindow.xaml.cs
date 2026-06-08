using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System;
using DownloadManagerH.Windows;
using DownloadManagerH.Models;
using System.Diagnostics;
using DownloadManagerH.Windows.Dialog;

namespace DownloadManagerH.Windows
{
    public partial class DownloadDetailsWindow : Window
    {
        private readonly DownloadItem _item;
        private readonly DispatcherTimer _timer;
        private DispatcherTimer? _autoCloseTimer;
        public DownloadDetailsWindow(DownloadItem item)
        {
            InitializeComponent();
            this.Title = $"📝 {item.FileName}";
            _item = item;
            this.DataContext = _item; // برای data binding
            txtFileName.Text = item.FileName;
            txtUrl.Text = item.Url;
            UpdateFields();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateFields();
            _timer.Start();
            this.Closed += (s, e) => _timer.Stop();
            dgParts.ItemsSource = new ObservableCollection<DownloadPart>(item.Parts);
            _item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_item.Status) && _item.Status == DownloadStatus.Completed)
                {
                    // Auto-close after 2 seconds if still open
                    _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    _autoCloseTimer.Tick += (s2, e2) => { _autoCloseTimer.Stop(); this.Close(); };
                    _autoCloseTimer.Start();
                }
            };
        }
        private void UpdateFields()
        {
            //_item.Progress = _item.TotalBytes / _item.DownloadedBytes * 100;
            txtTotalSize.Text = FormatSize(_item.TotalBytes);
            txtDownloadedSize.Text = FormatSize(_item.DownloadedBytes);
            txtSpeed.Text = _item.Speed;
            txtRemaining.Text = CalcRemainingTime();
            mainProgress.Value = _item.Progress;

            //txtMainProgress.Text = $"پیشرفت کلی: {_item.Progress:F2}%";
            mainProgress.ToolTip = $"پیشرفت کلی: {_item.Progress:F2}%";
            dgParts.ItemsSource = new ObservableCollection<DownloadPart>(_item.Parts);
            
            // بروزرسانی وضعیت قابلیت توقف
            if (_item.CanResume)
            {
                txtCanPause.Text = "بله";
                txtCanPause.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                txtCanPause.Text = "خیر";
                txtCanPause.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }
        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024 * 1024.0):F2} گیگابایت";
            if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024.0):F2} مگابایت";
            if (bytes >= 1024) return $"{bytes / 1024.0:F2} کیلوبایت";
            return $"{bytes} بایت";
        }
        private string CalcRemainingTime()
        {
            if (_item.DownloadedBytes <= 0 || string.IsNullOrWhiteSpace(_item.Speed) || !_item.Speed.Contains("B")) return "-";
            double speed = 0;
            var s = _item.Speed.ToLower();
            if (s.Contains("kb")) _=double.TryParse(s.Replace("kb/s", "").Trim(), out speed);
            else if (s.Contains("mb")) { _=double.TryParse(s.Replace("mb/s", "").Trim(), out speed); speed *= 1024; }
            else if (s.Contains("gb")) { _=double.TryParse(s.Replace("gb/s", "").Trim(), out speed); speed *= 1024 * 1024; }
            else _=double.TryParse(s.Replace("b/s", "").Trim(), out speed);
            if (speed <= 0) return "-";
            var remain = (_item.TotalBytes - _item.DownloadedBytes) / speed;
            var ts = TimeSpan.FromSeconds(remain);
            return ts.TotalSeconds > 0 ? ts.ToString("hh\\:mm\\:ss") : "-";
        }
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_item.SavePath) && !string.IsNullOrWhiteSpace(_item.FileName))
            {
                var file = System.IO.Path.Combine(_item.SavePath, _item.FileName);
                if (System.IO.File.Exists(file))
                    Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            }
        }
        private void BtnOpenWith_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_item.SavePath) && !string.IsNullOrWhiteSpace(_item.FileName))
            {
                var file = System.IO.Path.Combine(_item.SavePath, _item.FileName);
                if (System.IO.File.Exists(file))
                    Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {file}") { UseShellExecute = true });
            }
        }
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_item.SavePath) && System.IO.Directory.Exists(_item.SavePath))
                Process.Start("explorer.exe", _item.SavePath);
        }

        private async void BtnToggleDownload_Click(object sender, RoutedEventArgs e)
        {
            var manager = MainWindow.Me?.manager;
            if (manager == null) return;

            switch (_item.Status)
            {
                case DownloadStatus.Pending:
                case DownloadStatus.Stopped:
                    var success = await manager.StartDownloadAsync(_item);
                    break;
                    
                case DownloadStatus.Downloading:
                    if (!_item.CanResume)
                    {
                        var result = CustomMessageBox.Show("این دانلود قابل توقف نیست و در صورت توقف باید از ابتدا شروع شود. آیا مطمئن هستید؟", "توقف دانلود", CustomMessageBoxType.YesNo);
                        if (result != CustomMessageBoxResult.Yes) return;
                    }
                    manager.PauseDownload(_item);
                    manager.SaveDownloads();
                    break;
                    
                case DownloadStatus.Paused:
                    _=Models.DownloadManager.ResumeDownload(_item);
                    manager.SaveDownloads();
                    break;
                    
                case DownloadStatus.Failed:
                    var retrySuccess = await manager.StartDownloadAsync(_item);
                    break;
            }
        }
    }
} 