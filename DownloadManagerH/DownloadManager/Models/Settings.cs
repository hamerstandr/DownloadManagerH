using System;
using System.IO;

namespace DownloadManagerH.Models
{
    public static class Settings
    {
        public static int ParallelParts { get; set; } = 4; // تعداد بخش‌های موازی پیش‌فرض
        public static int MaxConcurrentDownloadsLimit { get; set; } = 5; // حداکثر دانلود همزمان
        public static bool EnableStartup { get; set; } = false; // فعال بودن استارت‌آپ
        public static bool MonitorClipboard { get; set; } = true; // مانیتور کلیپ‌بورد
        public static bool AddDownloadsDirectly { get; set; } = false; // افزودن مستقیم دانلودهای افزونه
        
        public static string ThemeColor { get; set; } = "#4caf50"; // رنگ تم یا رنگ دکمه‌ها
        public static int CountConctionDownloads { get; set; } = 3; // تعداد اتصالات موازی
        public static string DefaultDownloadPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            App.appName // نام برنامه خود را اینجا قرار دهید
        );

        public static string TempDirectory => Path.Combine(DataDirectory, "temp");
    }
} 