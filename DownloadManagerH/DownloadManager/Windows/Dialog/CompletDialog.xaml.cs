using DownloadManagerH.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DownloadManagerH.Windows.Dialog
{
    /// <summary>
    /// Interaction logic for CompletDialog.xaml
    /// </summary>
    public partial class CompletDialog : Window
    {
        public DownloadItem _item;
        public CompletDialog(DownloadItem item)
        {
            _item=item;
            DataContext = this;
            InitializeComponent();
            txtFileName.Text = item.FileName;
            txtFilePath.Text = item.SavePath;
            txtUrl.Text = item.Url;
            txtTotalSize.Text = FormatSize(_item.TotalBytes);
            txtDownloadedSize.Text = FormatSize(_item.DownloadedBytes);
        }
        public static void Show(DownloadItem item)
        {
            var win = new CompletDialog(item)
            {
                Owner = App.Current.MainWindow
            };
            win.ShowDialog();
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
        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024 * 1024.0):F2} گیگابایت";
            if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024.0):F2} مگابایت";
            if (bytes >= 1024) return $"{bytes / 1024.0:F2} کیلوبایت";
            return $"{bytes} بایت";
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
