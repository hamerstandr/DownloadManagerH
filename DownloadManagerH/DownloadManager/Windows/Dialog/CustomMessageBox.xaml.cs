using System.Windows;
using System.Windows.Media.Imaging;
using System;

namespace DownloadManagerH.Windows.Dialog
{
    public enum CustomMessageBoxResult { OK, Cancel, Yes, No }
    public enum CustomMessageBoxType { OK, OKCancel, YesNo, Error, Warning, Success }
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBoxResult Result { get; private set; } = CustomMessageBoxResult.Cancel;
        public CustomMessageBox(string message, string title = "پیام", CustomMessageBoxType type = CustomMessageBoxType.OK, string details = null)
        {
            InitializeComponent();
            this.Title = title;
            txtTitle.Text = title;
            lblMessage.Text = message;
            this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/icons8_chevron_down2.ico"));
            // تنظیم آیکون بر اساس نوع پیام
            switch (type)
            {
                case CustomMessageBoxType.Error:
                    imgIcon.Source = System.Windows.Application.Current.Resources["ErrorIcon"] as System.Windows.Media.ImageSource;
                    break;
                case CustomMessageBoxType.Warning:
                    imgIcon.Source = System.Windows.Application.Current.Resources["WarningIcon"] as System.Windows.Media.ImageSource;
                    break;
                case CustomMessageBoxType.Success:
                    imgIcon.Source = System.Windows.Application.Current.Resources["SuccessIcon"] as System.Windows.Media.ImageSource;
                    break;
                default:
                    imgIcon.Source = System.Windows.Application.Current.Resources["InfoIcon"] as System.Windows.Media.ImageSource;
                    break;
            }
            // نمایش جزئیات خطا اگر وجود داشته باشد
            if (!string.IsNullOrEmpty(details))
            {
                txtDetails.Text = details;
                scrollDetails.Visibility = Visibility.Visible;
            }
            else
            {
                scrollDetails.Visibility = Visibility.Collapsed;
            }
            btnOk.Visibility = (type == CustomMessageBoxType.OK || type == CustomMessageBoxType.OKCancel || type == CustomMessageBoxType.Error || type == CustomMessageBoxType.Warning || type == CustomMessageBoxType.Success) ? Visibility.Visible : Visibility.Collapsed;
            btnCancel.Visibility = (type == CustomMessageBoxType.OKCancel) ? Visibility.Visible : Visibility.Collapsed;
            btnYes.Visibility = (type == CustomMessageBoxType.YesNo) ? Visibility.Visible : Visibility.Collapsed;
            btnNo.Visibility = (type == CustomMessageBoxType.YesNo) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.OK;
            this.DialogResult = true;
            this.Close();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.Cancel;
            this.DialogResult = false;
            this.Close();
        }
        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.Yes;
            this.DialogResult = true;
            this.Close();
        }
        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.No;
            this.DialogResult = false;
            this.Close();
        }
        public static CustomMessageBoxResult Show(string message, string title = "پیام", CustomMessageBoxType type = CustomMessageBoxType.OK, string details = null)
        {
            var win = new CustomMessageBox(message, title, type, details);
            win.ShowDialog();
            return win.Result;
        }
        public static CustomMessageBoxResult ShowMessageBox(string message, string title = "پیام", CustomMessageBoxType type = CustomMessageBoxType.OK, string details = null)
        {
            var win = new CustomMessageBox(message, title, type, details);
            win.ShowDialog();
            return win.Result;
        }
    }
} 