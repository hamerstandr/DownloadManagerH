using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using System.Linq;
using System.Windows.Controls;
using System.ComponentModel;
using DownloadManagerH.Windows.Dialog;
using DownloadManagerH.Models;
using DownloadManagerH;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace DownloadManagerH.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //public DownloadManager Getmanager() { return (DownloadManager)DataContext; }
        internal readonly Models.DownloadManager manager = new();
        private readonly ObservableCollection<DownloadItem> downloadList;
        private NativeMessagingHost? nativeMessagingHost;
        private readonly ClipboardMonitor clipboardMonitor;
        public ObservableCollection<GroupMenuItem> GroupMenuItems { get; set; } = new ObservableCollection<GroupMenuItem>();
        public int DownloadCount => manager.DownloadCount;
        public double AverageSpeed => manager.AverageSpeed;
        public List<DownloadItem> Downloads => manager.Downloads;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainWindow()
        {
            InitializeComponent();
            Me = this;
            manager.LoadDownloads();
            downloadList = new ObservableCollection<DownloadItem>(manager.Downloads);
            dgDownloads.ItemsSource = downloadList;

            // اضافه کردن event handlers
            manager.DownloadAdded += Manager_DownloadAdded;
            manager.DownloadRemoved += Manager_DownloadRemoved;
            manager.DownloadUpdated += Manager_DownloadUpdated;

            RefreshList();
            //add event dgDownloads
            //dgDownloads.MouseDoubleClick += DgDownloads_MouseDoubleClick;
            //dgDownloads.KeyDown += DgDownloads_KeyDown;
            //dgDownloads.LoadingRow += DgDownloads_LoadingRow; // برای مقداردهی سرعت
            //addon
            RefreshList();
            RefreshGroupMenuItems(); // عملی و داینامیک
            this.DataContext = this;
            this.Closing += MainWindow_Closing;
            
            // راه‌اندازی Native Messaging Host
            InitializeNativeMessaging();
            
            // مانیتور کلیپ‌بورد
            clipboardMonitor = new ClipboardMonitor();
            clipboardMonitor.IsEnabled = Settings.MonitorClipboard;
            clipboardMonitor.NewUrlFound += ClipboardMonitor_NewUrlFound;
            if (Settings.MonitorClipboard)
                clipboardMonitor.Start();
            
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            this.Hide();
        }

        private void DgDownloads_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            if (e.Row.Item is DownloadItem item)
            {
                // مقداردهی سرعت (در صورت نیاز)
                if (item.Status == DownloadStatus.Downloading)
                {
                    // فرض بر این است که DownloadManager مقدار سرعت را به‌روزرسانی می‌کند
                    // اگر نه، اینجا می‌توانید مقداردهی تستی انجام دهید
                    if (string.IsNullOrEmpty(item.Speed))
                        item.Speed = "0 KB/s";
                }
                else
                {
                    item.Speed = "-";
                }
            }
        }
        private static void ShowDownloadDetailsWindowDialog(DownloadItem item)
        {
            if (item.Status != DownloadStatus.Completed)
            {
                //item.ParallelParts=3;
                // اگر دانلود کامل شده است، نمایش جزئیات
                var win = new DownloadDetailsWindow(item);
                win.ShowDialog();
            }
            else// if (item.Status == DownloadStatus.Failed)
            {
                // اگر دانلود شکست خورده است، نمایش جزئیات و امکان شروع مجدد
                var retryWin = new CompletDialog(item);
                retryWin.ShowDialog();
            }
        }
        private void DgDownloads_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                ShowDownloadDetailsWindowDialog(item);


            }
        }

        private void DgDownloads_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && dgDownloads.SelectedItem is DownloadItem item)
            {
                ShowDownloadDetailsWindowDialog(item);
            }
        }

        private async void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog();
            if (dialog.ShowDialog() == true)
            {
                var item = new DownloadItem
                {
                    Url = dialog.Url,
                    FileName = dialog.FileName,
                    SavePath = dialog.SavePath,
                    Group = dialog.Group,
                    Status = DownloadStatus.Pending,
                    ScheduledTime = dialog.ScheduledTime,
                    Speed = "-",
                    ParallelParts=Settings.ParallelParts
                };
                AddDownloadWithGroupSync(item);
                
                // اگر کاربر دکمه شروع را زده باشد، دانلود را شروع کن
                if (dialog.ShouldStartDownload)
                {
                    var success = await manager.StartDownloadAsync(item);
                    dgDownloads.Items.Refresh();
                    OnPropertyChanged(nameof(AverageSpeed));
                    
                    // نمایش پیام بر اساس نتیجه
                    if (!success)
                    {
                        // پیام خطا قبلاً در DownloadManager نمایش داده شده است
                        // فقط UI را بروزرسانی می‌کنیم
                    }
                }
            }
        }

        private void DgDownloads_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                var menu = dgDownloads.ContextMenu;
                //menu.Items.Filter
                var menuItems=menu.Items;
                var menuStart = (MenuItem)menuItems[0]; //menu.FindName("menuStart");
                var menuPause = (MenuItem)menuItems[1]; //menu.FindName("menuPause");
                var menuResume = (MenuItem)menuItems[2]; //menu.FindName("menuResume");
                var menuRemove = (MenuItem)menuItems[3]; //menu.FindName("menuRemove");
                var menuOpenFolder = (MenuItem)menuItems[4]; //menu.FindName("menuOpenFolder");
                var menuOpenFile = (MenuItem)menuItems[5]; //menu.FindName("menuOpenFile");
                var menuDetails = (MenuItem)menuItems[6]; //menu.FindName("menuDetails");

                bool canStart = item.Status == DownloadStatus.Pending || item.Status == DownloadStatus.Stopped || item.Status == DownloadStatus.Failed;
                bool canPause = item.Status == DownloadStatus.Downloading;
                bool canResume = item.Status == DownloadStatus.Paused;
                bool isCompleted = item.Status == DownloadStatus.Completed;

                menuStart.IsEnabled = canStart;
                menuPause.IsEnabled = canPause;
                menuResume.IsEnabled = canResume;
                menuOpenFolder.IsEnabled = isCompleted || !string.IsNullOrWhiteSpace(item.SavePath);
                menuOpenFile.IsEnabled = isCompleted;
            }
            else
            {
                // No item selected, disable all
                foreach (var menuItem in dgDownloads.ContextMenu.Items.OfType<MenuItem>())
                {
                    menuItem.IsEnabled = false;
                }
            }
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                manager.RemoveDownload(item);
                downloadList.Remove(item);
                manager.SaveDownloads();
                OnPropertyChanged(nameof(DownloadCount));
                OnPropertyChanged(nameof(AverageSpeed));
            }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                var win = new DownloadDetailsWindow(item);
                win.Show();
                var success = await manager.StartDownloadAsync(item);
                dgDownloads.Items.Refresh();
                OnPropertyChanged(nameof(AverageSpeed));
                
                // پیام‌های خطا و موفقیت قبلاً در DownloadManager نمایش داده شده‌اند
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                if (!item.CanResume)
                {
                    var result = CustomMessageBox.Show("این دانلود قابل توقف نیست و در صورت توقف باید از ابتدا شروع شود. آیا مطمئن هستید؟", "توقف دانلود", CustomMessageBoxType.YesNo);
                    if (result != CustomMessageBoxResult.Yes) return;
                }
                manager.PauseDownload(item);
                dgDownloads.Items.Refresh();
                // حذف SaveDownloads تکراری چون در PauseDownload انجام شده
                OnPropertyChanged(nameof(AverageSpeed));
            }
        }

        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                Models.DownloadManager.ResumeDownload(item);
                dgDownloads.Items.Refresh();
                manager.SaveDownloads();
            }
        }

        private async void btnToggleDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadItem item)
            {
                switch (item.Status)
                {
                    case DownloadStatus.Pending:
                    case DownloadStatus.Stopped:
                        var win = new DownloadDetailsWindow(item);
                        win.Show();
                        var success = await manager.StartDownloadAsync(item);
                        dgDownloads.Items.Refresh();
                        OnPropertyChanged(nameof(AverageSpeed));
                        break;
                        
                    case DownloadStatus.Downloading:
                        if (!item.CanResume)
                        {
                            var result = CustomMessageBox.Show("این دانلود قابل توقف نیست و در صورت توقف باید از ابتدا شروع شود. آیا مطمئن هستید؟", "توقف دانلود", CustomMessageBoxType.YesNo);
                            if (result != CustomMessageBoxResult.Yes) return;
                        }
                        manager.PauseDownload(item);
                        dgDownloads.Items.Refresh();
                        // حذف SaveDownloads تکراری چون در PauseDownload انجام شده
                        OnPropertyChanged(nameof(AverageSpeed));
                        break;
                        
                    case DownloadStatus.Paused:
                        Models.DownloadManager.ResumeDownload(item);
                        dgDownloads.Items.Refresh();
                        manager.SaveDownloads();
                        break;
                        
                    case DownloadStatus.Failed:
                        var retryWin = new DownloadDetailsWindow(item);
                        retryWin.Show();
                        var retrySuccess = await manager.StartDownloadAsync(item);
                        dgDownloads.Items.Refresh();
                        OnPropertyChanged(nameof(AverageSpeed));
                        break;
                }
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, System.Text.Json.JsonSerializer.Serialize(manager.Downloads));
                CustomMessageBox.Show("لیست دانلودها با موفقیت ذخیره شد.", "ذخیره", CustomMessageBoxType.OK);
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                var json = File.ReadAllText(dialog.FileName);
                var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<DownloadItem>>(json);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        manager.AddDownload(item);
                        downloadList.Add(item);
                    }
                    manager.SaveDownloads();
                    dgDownloads.Items.Refresh();
                    CustomMessageBox.Show("لیست دانلودها با موفقیت وارد شد.", "واردسازی", CustomMessageBoxType.OK);
                }
            }
        }

        private void btnStartup_Click(object sender, RoutedEventArgs e)
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            if (!StartupHelper.IsInStartup())
            {
                StartupHelper.AddToStartup(exePath);
                CustomMessageBox.Show("برنامه به استارتاپ ویندوز اضافه شد.", "استارتاپ", CustomMessageBoxType.OK);
            }
            else
            {
                StartupHelper.RemoveFromStartup();
                CustomMessageBox.Show("برنامه از استارتاپ ویندوز حذف شد.", "استارتاپ", CustomMessageBoxType.OK);
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            if (win.ShowDialog() == true)
            {
                // بروزرسانی Semaphore برای دانلود همزمان
                RefreshList();
                //manager = new DownloadManager();
                //manager.LoadDownloads();
                //downloadList = new ObservableCollection<DownloadItem>(manager.Downloads);
                //dgDownloads.ItemsSource = downloadList;
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            string help = "راهنمای استفاده از دانلود منجر حامد 🚀\n\n" +
                "۱. افزودن دانلود: روی دکمه ➕ کلیک کنید و لینک را وارد نمایید.\n" +
                "۲. شروع/توقف دانلود: از دکمه‌های ▶️ و ⏸️ استفاده کنید.\n" +
                "۳. مدیریت گروه‌ها: از منوی 🗂️ استفاده کنید.\n" +
                "۴. تنظیمات: از منوی ⚙️ برای شخصی‌سازی برنامه استفاده کنید.\n" +
                "🧩 افزونه مرورگر: پس از نصب، روی هر لینک راست‌کلیک و گزینه 'ارسال به دانلود منجر حامد 🚀' را انتخاب کنید.\n" +
                "💡 برای دریافت راهنمای بیشتر، به README.md مراجعه کنید.";
            CustomMessageBox.Show(help, "راهنمای برنامه", CustomMessageBoxType.OK);
        }

        private void btnResourceTest_Click(object sender, RoutedEventArgs e)
        {
            var testWindow = new ResourceTestWindow();
            testWindow.ShowDialog();
        }

        private void RefreshGroupMenuItems()
        {
            GroupMenuItems.Clear();
            // Load groups from groups.json
            var groupsFile = "groups.json";
            if (File.Exists(groupsFile))
            {
                var json = File.ReadAllText(groupsFile);
                var groups = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<GroupManagerWindow.DownloadGroup>>(json);
                if (groups != null)
                {
                    foreach (var g in groups)
                    {
                        // Pick an emoji/color for each group (for demo: blue/green/...) based on index or color)
                        string emoji = "🟦";
                        if (g.Color == "#333" || g.Color == "#0000ff") emoji = "🟦";
                        else if (g.Color == "#4caf50" || g.Color == "#00ff00") emoji = "🟩";
                        else if (g.Color == "#ffeb3b" || g.Color == "#ffff00") emoji = "🟨";
                        else if (g.Color == "#f44336" || g.Color == "#ff0000") emoji = "🟥";
                        else emoji = "🟪";
                        GroupMenuItems.Add(new GroupMenuItem {
                            GroupName = g.Name,
                            DisplayName = $"{emoji} {g.Name}",
                            ToolTip = $"انتقال به {g.Name}"
                        });
                    }
                }
            }
        }

        private void MenuGroupManager_Click(object sender, RoutedEventArgs e)
        {
            var win = new GroupManagerWindow();
            win.ShowDialog();
            RefreshGroupMenuItems();
        }

        private void MenuChangeGroup_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item && sender is MenuItem mi && mi.DataContext is GroupMenuItem group)
            {
                string oldGroup = item.Group ?? "پیش‌فرض";
                string newGroup = group.GroupName;
                if (oldGroup == newGroup) return;
                item.Group = newGroup;
                manager.SaveDownloads();

                // همگام‌سازی با groups.json
                var groupsFile = "groups.json";
                List<GroupManagerWindow.DownloadGroup> groups = new();
                if (File.Exists(groupsFile))
                {
                    var json = File.ReadAllText(groupsFile);
                    groups = System.Text.Json.JsonSerializer.Deserialize<List<GroupManagerWindow.DownloadGroup>>(json) ?? new();
                }
                // حذف از گروه قبلی
                var oldGroupObj = groups.FirstOrDefault(g => g.Name == oldGroup);
                oldGroupObj?.Downloads.RemoveAll(d => d.Url == item.Url);
                // افزودن به گروه جدید
                var newGroupObj = groups.FirstOrDefault(g => g.Name == newGroup);
                if (newGroupObj == null)
                {
                    newGroupObj = new GroupManagerWindow.DownloadGroup { Name = newGroup };
                    groups.Add(newGroupObj);
                }
                if (newGroupObj.Downloads.All(d => d.Url != item.Url))
                    newGroupObj.Downloads.Add(item);
                File.WriteAllText(groupsFile, System.Text.Json.JsonSerializer.Serialize(groups));

                // به‌روزرسانی UI
                dgDownloads.Items.Refresh();
            }
        }

        private void MenuDetails_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                ShowDownloadDetailsWindowDialog(item);
            }
        }
        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.SavePath) && Directory.Exists(item.SavePath))
                    System.Diagnostics.Process.Start("explorer.exe", item.SavePath);
            }
        }

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                var file = System.IO.Path.Combine(item.SavePath, item.FileName);
                if (System.IO.File.Exists(file))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            }
        }
        private void MenuOpenWith_Click(object sender, RoutedEventArgs e)
        {
            if (dgDownloads.SelectedItem is DownloadItem item)
            {
                var file = System.IO.Path.Combine(item.SavePath, item.FileName);
                if (System.IO.File.Exists(file))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {file}") { UseShellExecute = true });
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show("آیا مطمئن هستید که می‌خواهید برنامه را ببندید؟", "خروج", CustomMessageBoxType.OKCancel);
            if (result == CustomMessageBoxResult.OK)
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// مقداردهی اولیه Native Messaging Host
        /// </summary>
        private async void InitializeNativeMessaging()
        {
            try
            {
                var logger = DownloadManagerH.Models.Logging.LoggerFactory.GetDefaultLogger();
                
                // ایجاد Native Messaging Host
                var host = new NativeMessagingHost(manager, logger);
                
                // ثبت event handlers
                host.MessageReceived += OnNativeMessageReceived;
                host.ConnectionStateChanged += OnNativeMessagingConnectionStateChanged;
                host.ErrorOccurred += OnNativeMessagingError;
                
                // شروع Native Messaging Host
                await host.StartAsync();
                
                // ذخیره reference برای استفاده بعدی
                this.nativeMessagingHost = host;
                
                logger.LogInfo("Native Messaging Host initialized and started in MainWindow");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در راه‌اندازی Native Messaging Host: {ex.Message}");
                // در صورت خطا، برنامه همچنان کار می‌کند
            }
        }

        /// <summary>
        /// مدیریت پیام‌های دریافتی از Native Messaging
        /// </summary>
        private void OnNativeMessageReceived(object? sender, NativeMessageEventArgs e)
        {
            try
            {
                var logger = DownloadManagerH.Models.Logging.LoggerFactory.GetDefaultLogger();
                logger.LogInfo($"Native message received: {e.Message.Type} from {e.Browser}");
                
                // نمایش اعلان در UI thread
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        switch (e.Message.Type?.ToLower())
                        {
                            case "adddownload":
                                ShowNotification("دانلود منجر حامد", $"درخواست دانلود جدید از {e.Browser}", false);
                                RefreshList(); // بروزرسانی لیست دانلودها
                                break;
                                
                            case "interceptdownload":
                                ShowNotification("دانلود منجر حامد", $"دانلود هدایت شده از {e.Browser}", false);
                                RefreshList();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error handling native message in UI", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در پردازش پیام Native Messaging: {ex.Message}");
            }
        }

        /// <summary>
        /// مدیریت تغییر وضعیت اتصال Native Messaging
        /// </summary>
        private void OnNativeMessagingConnectionStateChanged(object? sender, ConnectionStateEventArgs e)
        {
            try
            {
                var logger = DownloadManagerH.Models.Logging.LoggerFactory.GetDefaultLogger();
                logger.LogInfo($"Native Messaging connection state changed: {e.IsConnected} ({e.Browser}) - {e.Message}");
                
                Dispatcher.Invoke(() =>
                {
                    if (e.IsConnected)
                    {
                        ShowNotification("Native Messaging", $"اتصال برقرار شد با {e.Browser}", false);
                    }
                    else
                    {
                        ShowNotification("Native Messaging", $"اتصال قطع شد از {e.Browser}", true);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در مدیریت تغییر وضعیت اتصال: {ex.Message}");
            }
        }

        /// <summary>
        /// مدیریت خطاهای Native Messaging
        /// </summary>
        private void OnNativeMessagingError(object? sender, NativeMessagingErrorEventArgs e)
        {
            try
            {
                var logger = DownloadManagerH.Models.Logging.LoggerFactory.GetDefaultLogger();
                logger.LogError($"Native Messaging error ({e.ErrorType}): {e.Message}", e.Exception);
                
                Dispatcher.Invoke(() =>
                {
                    ShowNotification("خطای Native Messaging", e.Message, true);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در مدیریت خطای Native Messaging: {ex.Message}");
            }
        }

        /// <summary>
        /// نمایش اعلان به کاربر
        /// </summary>
        private void ShowNotification(string title, string message, bool isError)
        {
            try
            {
                CustomMessageBox.Show(message, title, isError ? CustomMessageBoxType.Error : CustomMessageBoxType.Success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در نمایش اعلان: {ex.Message}");
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                // توقف Native Messaging Host
                nativeMessagingHost?.StopAsync().Wait(5000);
                nativeMessagingHost?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطا در توقف Native Messaging Host: {ex.Message}");
            }
            
            clipboardMonitor?.Stop();
            manager.SaveDownloads();
            manager.Dispose(); // Proper disposal of resources
            base.OnClosed(e);
        }

        private void ClipboardMonitor_NewUrlFound(string text)
        {
            Dispatcher.Invoke(() =>
            {
                var urls = ParseLinksFromBody(text);
                
                if (urls.Count == 1)
                {
                    //برسی وجود لینک در لیست دانلود
                    if (manager == null) return;
                    if (manager.Downloads.FindIndex(i => i.Url==urls[0])!=-1)
                    {
                        //MessageBox.Show("خطا : فایل وجود دارد");
                        return;
                    }
                    if (Settings.AddDownloadsDirectly)
                    {
                        var item = new DownloadItem
                        {
                            Url = urls[0],
                            FileName = System.IO.Path.GetFileName(new Uri(urls[0]).AbsolutePath),
                            SavePath = Settings.DefaultDownloadPath,
                            Status = DownloadStatus.Pending,
                            Speed = "-",
                            ParallelParts=Settings.ParallelParts
                        };
                        AddDownloadWithGroupSync(item);
                        CustomMessageBox.Show("لینک جدید از کلیپ‌بورد به لیست دانلودها اضافه شد!", "افزودن دانلود", CustomMessageBoxType.Success);
                    }
                    else
                    {
                        var dialog = new Windows.Dialog.InputDialog();
                        dialog.txtUrl.Text = urls[0];
                        dialog.txtFileName.Text = System.IO.Path.GetFileName(new Uri(urls[0]).AbsolutePath);
                        dialog.txtSavePath.Text = Settings.DefaultDownloadPath;
                        if (dialog.ShowDialog() == true)
                        {
                            var item = new DownloadItem
                            {
                                Url = dialog.Url,
                                FileName = dialog.FileName,
                                SavePath = dialog.SavePath,
                                Group = dialog.Group,
                                Status = DownloadStatus.Pending,
                                Speed = "-",
                                ParallelParts=Settings.ParallelParts
                            };
                            AddDownloadWithGroupSync(item);
                            CustomMessageBox.Show("دانلود جدید از کلیپ‌بورد اضافه شد!", "افزودن دانلود", CustomMessageBoxType.Success);
                        }
                    }
                }
                else if (urls.Count > 1)
                {
                    var groups = manager.GetGroups().ToList();
                    if (!groups.Contains("پیش‌فرض")) groups.Insert(0, "پیش‌فرض");
                    var dialog = new BulkAddDialog(urls, groups, Settings.DefaultDownloadPath);
                    if (dialog.ShowDialog() == true)
                    {
                        var selected = dialog.GetSelectedLinks();
                        foreach (var link in selected)
                        {
                            var item = new DownloadItem
                            {
                                Url = link.Url,
                                FileName = System.IO.Path.GetFileName(new Uri(link.Url).AbsolutePath),
                                SavePath = dialog.SavePath,
                                Group = dialog.SelectedGroup,
                                Status = DownloadStatus.Pending,
                                Speed = "-",
                                ParallelParts=Settings.ParallelParts
                            };
                            AddDownloadWithGroupSync(item);
                        }
                        CustomMessageBox.Show($"{selected.Count} دانلود جدید از کلیپ‌بورد اضافه شد!", "افزودن گروهی", CustomMessageBoxType.Success);
                    }
                }
                else
                {
                    CustomMessageBox.Show("هیچ لینک معتبری در کلیپ‌بورد یافت نشد!", "خطا", CustomMessageBoxType.Error);
                }
                if (urls.Count>0)
                    RefreshList();
            });
        }

        /// <summary>
        /// افزودن دانلود جدید به لیست اصلی و گروه مربوطه (در groups.json)
        /// </summary>
        private void AddDownloadWithGroupSync(DownloadItem item)
        {
            
            
            // افزودن به لیست اصلی
            manager.AddDownload(item);
            downloadList.Add(item);
            manager.SaveDownloads();
            OnPropertyChanged(nameof(DownloadCount));
            OnPropertyChanged(nameof(AverageSpeed));

            // افزودن به گروه مربوطه در groups.json
            var groupsFile = "groups.json";
            List<GroupManagerWindow.DownloadGroup> groups = new();
            if (File.Exists(groupsFile))
            {
                var json = File.ReadAllText(groupsFile);
                groups = System.Text.Json.JsonSerializer.Deserialize<List<GroupManagerWindow.DownloadGroup>>(json) ?? new();
            }
            var group = groups.FirstOrDefault(g => g.Name == (item.Group ?? "پیش‌فرض"));
            if (group == null)
            {
                group = new GroupManagerWindow.DownloadGroup { Name = item.Group ?? "پیش‌فرض" };
                groups.Add(group);
            }
            if (group.Downloads.All(d => d.Url != item.Url))
                group.Downloads.Add(item);
            File.WriteAllText(groupsFile, System.Text.Json.JsonSerializer.Serialize(groups));
        }

        // استخراج لینک‌ها از متن (لینک تکی، آرایه، متن چند خطی، HTML، JSON)
        private System.Collections.Generic.List<string> ParseLinksFromBody(string body)
        {
            var links = new System.Collections.Generic.List<string>();
            try
            {
                // اگر JSON array
                if (body.TrimStart().StartsWith("["))
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);
                    if (arr != null) links.AddRange(arr);
                }
                // اگر JSON object با فیلد links
                else if (body.TrimStart().StartsWith("{"))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("links", out var linksProp) && linksProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var l in linksProp.EnumerateArray())
                            if (l.ValueKind == System.Text.Json.JsonValueKind.String)
                                links.Add(l.GetString());
                    }
                    else if (doc.RootElement.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        links.Add(urlProp.GetString());
                    }
                }
                // اگر HTML
                else if (body.Contains("<a ") || body.Contains("<A "))
                {
                    var matches = Regex.Matches(body, "href=[\"']([^\"'>]+)", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        var url = m.Groups[1].Value;
                        if (url.StartsWith("http://") || url.StartsWith("https://"))
                            links.Add(url);
                    }
                }
                // اگر متن چند خطی
                else if (body.Contains("\n") || body.Contains("\r"))
                {
                    var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                        if (line.StartsWith("http://") || line.StartsWith("https://"))
                            links.Add(line.Trim());
                }
                // اگر فقط یک لینک تکی
                else if (body.StartsWith("http://") || body.StartsWith("https://"))
                {
                    links.Add(body.Trim());
                }
            }
            catch { }
            return links.Distinct().ToList();
        }

        public void EnableClipboardMonitor(bool enable)
        {
            clipboardMonitor.IsEnabled = enable;
            if (enable)
                clipboardMonitor.Start();
            else
                clipboardMonitor.Stop();
        }
        internal static MainWindow? Me;
        public Models.DownloadManager DownloadManager => manager;
        private void Manager_DownloadAdded(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                if (!downloadList.Contains(item))
                    downloadList.Add(item);
                OnPropertyChanged(nameof(DownloadCount));
                OnPropertyChanged(nameof(AverageSpeed));
                RefreshGroupMenuItems();
            });
        }

        private void Manager_DownloadRemoved(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                downloadList.Remove(item);
                OnPropertyChanged(nameof(DownloadCount));
                OnPropertyChanged(nameof(AverageSpeed));
                RefreshGroupMenuItems();
            });
        }

        private void Manager_DownloadUpdated(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                var index = downloadList.IndexOf(item);
                if (index >= 0)
                {
                    downloadList[index] = item;
                }
                OnPropertyChanged(nameof(AverageSpeed));
                RefreshGroupMenuItems();
            });
        }

        internal void RefreshList()
        {
            Dispatcher.Invoke(() =>
            {
                downloadList.Clear();
                foreach (var item in manager.Downloads)
                {
                    downloadList.Add(item);
                }
                OnPropertyChanged(nameof(DownloadCount));
                OnPropertyChanged(nameof(AverageSpeed));
                RefreshGroupMenuItems();
            });
        }
    }

    
}

