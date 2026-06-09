using DownloadManager.Models;
using DownloadManagerH.Controls;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;
using DownloadManagerH.Models;
using DownloadManagerH.Models.Logging;
using DownloadManagerH.Services;
using DownloadManagerH.Services.PluginSystem;
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
        private TrafficWatchIntegrationService? _trafficWatchService;
        private NamedPipePluginServer? _pluginServer;
        private DownloadManager? _downloadManager;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // مقداردهی اولیه مدیر تم و اعمال تم پیش‌فرض
            InitializeThemeManager();
            
            // مقداردهی اولیه Native Messaging
            InitializeNativeMessaging();
            
            // مقداردهی اولیه TrafficWatch Integration
            InitializeTrafficWatchIntegration();
            
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
        /// مقداردهی اولیه یکپارچگی با TrafficWatch
        /// </summary>
        private void InitializeTrafficWatchIntegration()
        {
            try
            {
                var logger = LoggerFactory.GetDefaultLogger();
                
                // دریافت نمونه DownloadManager از MainWindow
                if (MainWindow.Me?.DownloadManager != null)
                {
                    _downloadManager = MainWindow.Me.DownloadManager;
                    
                    // فعال‌سازی یکپارچگی بر اساس تنظیمات (پیش‌فرض: فعال)
                    bool enableIntegration = true; // قابل تغییر به Settings.EnableTrafficWatchIntegration
                    int port = 9090; // قابل تغییر به Settings.TrafficWatchPort
                    
                    _trafficWatchService = new TrafficWatchIntegrationService(_downloadManager, logger);
                    _trafficWatchService.Initialize(enableIntegration, port);
                    
                    logger.LogInfo($"TrafficWatch Integration initialized on port {port}");
                }
                
                // شروع سرور Named Pipe برای افزونه‌ها
                _pluginServer = new NamedPipePluginServer(logger);
                _pluginServer.DataReceived += OnPluginDataReceived;
                _pluginServer.PluginRegistered += OnPluginRegistered;
                _pluginServer.PluginDisconnected += OnPluginDisconnected;
                _pluginServer.Start();
                
                logger.LogInfo("Named Pipe Plugin Server started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در مقداردهی اولیه TrafficWatch Integration: {ex.Message}");
                // در صورت خطا، برنامه همچنان کار می‌کند
            }
        }

        /// <summary>
        /// مدیریت رویداد دریافت داده از افزونه
        /// </summary>
        private void OnPluginDataReceived(object? sender, PluginDataReceivedEventArgs e)
        {
            try
            {
                var logger = LoggerFactory.GetDefaultLogger();
                // اینجا می‌توان داده‌های استریم شده از افزونه را پردازش کرد
                // مثلاً بروزرسانی UI داشبورد با اطلاعات موسیقی
                logger.LogDebug($"Data received from plugin {e.AddonId}: {e.Data}");
                
                // ارسال رویداد به ViewModel یا سایر بخش‌ها
                // این بخش بسته به معماری برنامه قابل تغییر است
            }
            catch (Exception ex)
            {
                var logger = LoggerFactory.GetDefaultLogger();
                logger.LogError("Error processing plugin data", ex);
            }
        }

        /// <summary>
        /// مدیریت رویداد ثبت نام افزونه
        /// </summary>
        private void OnPluginRegistered(object? sender, PluginRegisteredEventArgs e)
        {
            var logger = LoggerFactory.GetDefaultLogger();
            logger.LogInfo($"Plugin registered: {e.Plugin.Name} v{e.Plugin.Version} ({e.Plugin.Id})");
        }

        /// <summary>
        /// مدیریت رویداد قطع اتصال افزونه
        /// </summary>
        private void OnPluginDisconnected(object? sender, PluginDisconnectedEventArgs e)
        {
            var logger = LoggerFactory.GetDefaultLogger();
            logger.LogInfo($"Plugin disconnected: {e.PluginId}");
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
            // توقف سرویس TrafficWatch
            _trafficWatchService?.Dispose();
            
            // توقف سرور Named Pipe
            _pluginServer?.Dispose();
            
            trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
