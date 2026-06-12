using System.Windows;
using Microsoft.Win32;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.IO;
using System.Collections.Generic;
using System;
using DownloadManagerH.Windows.Dialog;
using DownloadManagerH.Models;

namespace DownloadManagerH.Windows.Dialog
{
    public partial class InputDialog : Window
    {
        public string Url => txtUrl.Text;
        public string FileName => txtFileName.Text;
        public string SavePath => txtSavePath.Text;
        public string Group => cmbGroup.SelectedItem?.ToString() ?? "پیش‌فرض";
        public DateTime? ScheduledTime
        {
            get { return null; }
        }
        public bool ShouldStartDownload { get; private set; } = false;
        private List<string> groups = new List<string>();
        
        public InputDialog()
        {
            InitializeComponent();
            this.Loaded+=(s, e) => { this.Focus();this.Activate(); };
            // مقداردهی گروه‌ها
            groups = LoadGroups();
            if (!groups.Contains("پیش‌فرض")) groups.Insert(0, "پیش‌فرض");
            cmbGroup.ItemsSource = groups;
            cmbGroup.SelectedIndex = 0;
            // مقداردهی مسیر پیش‌فرض
            txtSavePath.Text = Settings.DefaultDownloadPath;
            // بررسی کلیپ‌بورد
            var clipboard = System.Windows.Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(clipboard) && (clipboard.StartsWith("http://") || clipboard.StartsWith("https://")))
            {
                txtUrl.Text = clipboard;
                SetFileNameFromUrl(clipboard);
            }
            // رویداد تغییر آدرس برای مقداردهی نام فایل
            txtUrl.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtFileName.Text))
                    SetFileNameFromUrl(txtUrl.Text);
            };
        }
        
        private void SetFileNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    txtFileName.Text = "file";
                    return;
                }
                
                var name = Path.GetFileName(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(name)) name = "file";
                name = MakeUniqueFileName(name, txtSavePath.Text);
                txtFileName.Text = name;
            }
            catch
            {
                txtFileName.Text = "file";
            }
        }
        
        private string MakeUniqueFileName(string name, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) folder = Settings.DefaultDownloadPath;
            var path = Path.Combine(folder, name);
            var ext = Path.GetExtension(name);
            var fileNameOnly = Path.GetFileNameWithoutExtension(name);
            int i = 1;
            while (File.Exists(path))
            {
                name = $"{fileNameOnly}({i}){ext}";
                path = Path.Combine(folder, name);
                i++;
            }
            return name;
        }
        
        private List<string> LoadGroups()
        {
            // TODO: بارگذاری گروه‌ها از فایل یا تنظیمات
            return ["پیش‌فرض"];
        }
        
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInputs())
            {
                ShouldStartDownload = true;
                DialogResult = true;
                Close(); // بستن دیالوگ پس از تأیید
            }
        }
        
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInputs())
            {
                ShouldStartDownload = false;
                DialogResult = true;
                Close();
            }
        }
        
        private bool ValidateInputs()
        {
            // اعتبارسنجی ورودی‌ها
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                CustomMessageBox.Show("لطفاً آدرس دانلود را وارد کنید.", "خطا", CustomMessageBoxType.OK);
                txtUrl.Focus();
                return false;
            }
            
            if (!Uri.TryCreate(txtUrl.Text, UriKind.Absolute, out Uri uri))
            {
                CustomMessageBox.Show("آدرس وارد شده معتبر نیست. لطفاً یک آدرس کامل وارد کنید.", "خطا", CustomMessageBoxType.OK);
                txtUrl.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(txtFileName.Text))
            {
                CustomMessageBox.Show("لطفاً نام فایل را وارد کنید.", "خطا", CustomMessageBoxType.OK);
                txtFileName.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(txtSavePath.Text))
            {
                CustomMessageBox.Show("لطفاً مسیر ذخیره را انتخاب کنید.", "خطا", CustomMessageBoxType.OK);
                return false;
            }
            
            return true;
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            if (dialog.ShowDialog() == true)
            {
                txtSavePath.Text = Path.GetDirectoryName(dialog.FileName);
                if (string.IsNullOrWhiteSpace(txtFileName.Text))
                    txtFileName.Text = Path.GetFileName(dialog.FileName);
            }
        }
        
        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var input = CustomInputBox.ShowInputBox("نام گروه جدید را وارد کنید:", "افزودن گروه جدید");
            if (!string.IsNullOrWhiteSpace(input) && !groups.Contains(input))
            {
                groups.Add(input);
                cmbGroup.ItemsSource = null;
                cmbGroup.ItemsSource = groups;
                cmbGroup.SelectedItem = input;
            }
        }
    }
} 