using DownloadManagerH.Windows;
using System.Reflection;

namespace DownloadManagerH.Controls
{
    public class AppInfo
    {
        private static AppInfo? _instance;
        public static AppInfo Instance => _instance ??= new AppInfo();

        public string Title { get; }
        public string Version { get; }
        public string Author { get; }
        public string Company { get; }
        public string Copyright { get; }
        public string Description { get; }

        private AppInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // خواندن اطلاعات از اسمبلی
            Title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "دانلود منیجر حامد";
            Version = assembly.GetName().Version+"";
            Author = "حامد محمدی";
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "شرکت فناوری اطلاعات حامد";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "کپی‌رایت © 2023";
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "برنامه مدیریت دانلود پیشرفته";
        }

        public void ShowAboutWindow()
        {
            new AboutWindow().ShowDialog();
        }
    }
}