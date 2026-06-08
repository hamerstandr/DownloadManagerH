using Microsoft.Win32;

namespace DownloadManagerH.Windows
{
    public static class StartupHelper
    {
        private const string StartupKey = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "DownloadManagerH";

        public static void AddToStartup(string exePath)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
            {
                key?.SetValue(AppName, '"' + exePath + '"');
            }
        }

        public static void RemoveFromStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
            {
                key?.DeleteValue(AppName, false);
            }
        }

        public static bool IsInStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupKey, false))
            {
                return key?.GetValue(AppName) != null;
            }
        }
    }
} 