using System.Windows;
using System.IO;
using DownloadManagerH.Models;

namespace DownloadManagerH.Windows.Dialog
{
    public enum FileConflictResult { Overwrite, Rename, Cancel }
    
    public partial class FileConflictDialog : Window
    {
        public FileConflictResult Result { get; private set; } = FileConflictResult.Cancel;
        public string NewFileName { get; private set; } = "";
        
        public FileConflictDialog(DownloadItem item)
        {
            InitializeComponent();
            this.Title = "تعارض فایل";
            txtTitle.Text = "فایل از قبل وجود دارد";
            
            var finalFile = Path.Combine(item.SavePath, item.FileName);
            lblMessage.Text = $"فایل '{item.FileName}' از قبل در مسیر زیر وجود دارد:\n{finalFile}\n\nچه کاری می‌خواهید انجام دهید؟";
            
            btnOverwrite.Visibility = Visibility.Visible;
            btnRename.Visibility = Visibility.Visible;
            btnCancel.Visibility = Visibility.Visible;
        }
        
        private void BtnOverwrite_Click(object sender, RoutedEventArgs e)
        {
            Result = FileConflictResult.Overwrite;
            this.DialogResult = true;
            this.Close();
        }
        
        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            Result = FileConflictResult.Rename;
            this.DialogResult = true;
            this.Close();
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = FileConflictResult.Cancel;
            this.DialogResult = false;
            this.Close();
        }
        
        public static FileConflictResult Show(DownloadItem item, ref string newFileName)
        {
            var win = new FileConflictDialog(item);
            win.ShowDialog();
            
            if (win.Result == FileConflictResult.Rename)
            {
                // نمایش دیالوگ ورودی برای نام جدید
                var inputBox = new CustomInputBox("نام جدید برای فایل وارد کنید:", "تغییر نام فایل");
                inputBox.txtInput.Text = Path.GetFileNameWithoutExtension(item.FileName);
                inputBox.txtInput.SelectAll();
                
                if (inputBox.ShowDialog() == true)
                {
                    var newName = inputBox.txtInput.Text.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        var ext = Path.GetExtension(item.FileName);
                        newFileName = $"{newName}{ext}";
                        return FileConflictResult.Rename;
                    }
                }
                return FileConflictResult.Cancel;
            }
            
            return win.Result;
        }
    }
}
