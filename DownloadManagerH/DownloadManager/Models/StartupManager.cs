using DownloadManagerH.Windows.Dialog;
using Microsoft.Win32;
using System;
using System.Reflection;

namespace DownloadManagerH.Models
{
    public static class StartupManager
    {
        public static void SetStartup(bool enable)
        {
            string appName = App.appName;
            SetStartup(enable, appName);
        }

        public static void SetStartup(bool enable, string appName)
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;

                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)??throw new InvalidOperationException("نمی‌توان به کلید رجیستری دسترسی پیدا کرد.");
                if (enable)
                {
                    key.SetValue(appName, $"\"{path}\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowMessageBox("خطا در تنظیم استارت‌آپ: " + ex.Message);
            }
        }
    }
}