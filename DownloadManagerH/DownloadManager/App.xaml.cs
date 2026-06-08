using DownloadManager.Models;
using DownloadManagerH.Controls;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;
using DownloadManagerH.Models;
using DownloadManagerH.Models.Logging;
using Hardcodet.Wpf.TaskbarNotification;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace DownloadManagerH
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string appName = Assembly.GetExecutingAssembly().GetName().Name ?? "DownloadManagerH";
        private TaskbarIcon? trayIcon;
        private NativeMessagingRegistrar? nativeMessagingRegistrar;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // مقداردهی اولیه مدیر تم و اعمال تم پیش‌فرض
            InitializeThemeManager();
            
            // مقداردهی اولیه Native Messaging
            InitializeNativeMessaging();
            
            trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            if (trayIcon != null)
            {
                var menu = trayIcon.ContextMenu;
                if (menu != null)
                {
                    var showHide = (MenuItem)menu.Items[0];
                    var settings = (MenuItem)menu.Items[1];
                    var abuot = (MenuItem)menu.Items[3];
                    var exit = (MenuItem)menu.Items[5];
                    showHide.Click += (s, ev) => ToggleMainWindow();
                    settings.Click += (s, ev) => ShowSettings();
                    abuot.Click += (s, ev) => AppInfo.Instance.ShowAboutWindow(); ;
                    exit.Click += (s, ev) => TryExit();
                }
                trayIcon.TrayMouseDoubleClick += (s, ev) => ToggleMainWindow();
            }
        }

        /// <summary>
        /// مقداردهی اولیه مدیر تم
        /// </summary>
        private void InitializeThemeManager()
        {
            try
            {
                var themeManager = ThemeManager.Instance;
                
                // اعمال تم پیش‌فرض
                themeManager.ApplyDefaultTheme();
                
                // ثبت رویداد تغییر تم
                themeManager.ThemeChanged += OnThemeChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در مقداردهی اولیه مدیر تم: {ex.Message}");
            }
        }

        /// <summary>
        /// مقداردهی اولیه Native Messaging
        /// </summary>
        private async void InitializeNativeMessaging()
        {
            try
            {
                var logger = LoggerFactory.GetDefaultLogger();
                
                // ثبت Native Messaging host در رجیستری
                nativeMessagingRegistrar = new NativeMessagingRegistrar(logger);
                await nativeMessagingRegistrar.RegisterHostAsync();
                
                logger.LogInfo("Native Messaging Host registered successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در مقداردهی اولیه Native Messaging: {ex.Message}");
                // در صورت خطا در ثبت، برنامه همچنان کار می‌کند
            }
        }

        /// <summary>
        /// مدیریت رویداد تغییر تم
        /// </summary>
        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            try
            {
                // اطلاع‌رسانی به تمام پنجره‌های باز
                foreach (Window window in Windows)
                {
                    if (window is IThemeAware themeAware)
                    {
                        themeAware.OnThemeChanged(e.NewTheme);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در اعمال تغییر تم: {ex.Message}");
            }
        }
        private void ToggleMainWindow()
        {
            var main = Current.MainWindow;
            if (main == null) return;
            if (main.Visibility == Visibility.Visible)
                main.Hide();
            else
            {
                main.Show();
                main.WindowState = WindowState.Normal;
                main.Activate();
            }
        }
        private void ShowSettings()
        {
            Application.Current.Dispatcher.Invoke(() => {
                var win = new SettingsWindow();
                win.ShowDialog();
            });
        }
        private void TryExit()
        {
            Application.Current.Dispatcher.Invoke(() => {
                var result = CustomMessageBox.Show("آیا مطمئن هستید که می‌خواهید برنامه را ببندید؟", "خروج", CustomMessageBoxType.OKCancel);
                if (result == CustomMessageBoxResult.OK)
                {
                    trayIcon?.Dispose();
                    Current.Shutdown();
                }
            });
        }
        protected override void OnExit(ExitEventArgs e)
        {
            trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
