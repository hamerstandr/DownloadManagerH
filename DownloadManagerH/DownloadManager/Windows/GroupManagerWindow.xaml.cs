using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Text.Json;
using System.IO;
using System;
using DownloadManagerH.Models;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;
using System.Windows.Controls.Primitives;

namespace DownloadManagerH.Windows
{
    public partial class GroupManagerWindow : Window
    {
        public class DownloadGroup
        {
            public string Name { get; set; } = "نام گروه";
            public string Color { get; set; } = "#000000";
            public int Concurrent { get; set; } = 1;
            public string ScheduleType { get; set; } = "زمان"; // "زمان"، "هفتگی"، "تاریخ و زمان"
            public string ScheduleTime { get; set; } = "00:00";// HH:mm
            public List<string> WeeklyDays { get; set; } = []; // e.g. ["ش", "ی", ...]
            public string WeeklyTime { get; set; } = "00:00"; // HH:mm
            public DateTime? ScheduleDate { get; set; }
            public string ScheduleDateTimeTime { get; set; } = "00:00";// HH:mm
            public List<DownloadItem> Downloads { get; set; } = new List<DownloadItem>();
        }
        private List<DownloadGroup> groups = new List<DownloadGroup>();
        private const string GroupsFile = "groups.json";
        private Models.DownloadManager manager;

        public GroupManagerWindow()
        {
            InitializeComponent();
            //var m=MainWindow.Me.manager;
            manager = ((MainWindow)Application.Current.MainWindow).manager;
            manager.DownloadAdded += Manager_DownloadAdded;
            manager.DownloadRemoved += Manager_DownloadRemoved;
            manager.DownloadUpdated += Manager_DownloadUpdated;
            LoadGroups();
        }

        private void Manager_DownloadAdded(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(item.Group))
                {
                    var group = groups.FirstOrDefault(g => g.Name == item.Group);
                    if (group != null && !group.Downloads.Contains(item))
                    {
                        group.Downloads.Add(item);
                        SaveGroups();
                        RefreshGroupList();
                    }
                }
            });
        }

        private void Manager_DownloadRemoved(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                var group = groups.FirstOrDefault(g => g.Downloads.Contains(item));
                if (group != null)
                {
                    group.Downloads.Remove(item);
                    SaveGroups();
                    RefreshGroupList();
                }
            });
        }

        private void Manager_DownloadUpdated(object? sender, DownloadItem item)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshGroupList();
            });
        }

        private void RefreshGroupList()
        {
            if (lstGroups.SelectedItem is DownloadGroup group)
            {
                lstGroupDownloads.ItemsSource = null;
                lstGroupDownloads.ItemsSource = group.Downloads;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // حذف event handlers
            manager.DownloadAdded -= Manager_DownloadAdded;
            manager.DownloadRemoved -= Manager_DownloadRemoved;
            manager.DownloadUpdated -= Manager_DownloadUpdated;
        }
        private void SaveGroups()
        {
            var json = JsonSerializer.Serialize(groups);
            File.WriteAllText(GroupsFile, json);
        }
        private void LoadGroups()
        {
            if (File.Exists(GroupsFile))
            {
                var json = File.ReadAllText(GroupsFile);
                var loadedGroups = JsonSerializer.Deserialize<List<DownloadGroup>>(json) ?? new List<DownloadGroup>();
                // همسان‌سازی رفرنس DownloadItemها با لیست اصلی
                var mainDownloads = ((MainWindow)Application.Current.MainWindow).Downloads;
                foreach (var g in loadedGroups)
                {
                    var syncedList = new List<DownloadItem>();
                    foreach (var item in g.Downloads)
                    {
                        var mainItem = mainDownloads.FirstOrDefault(d => d.Url == item.Url);
                        if (mainItem != null)
                            syncedList.Add(mainItem);
                    }
                    g.Downloads = syncedList;
                }
                groups = loadedGroups;
            }
            else
            {
                groups = new List<DownloadGroup> {
                    new DownloadGroup { Name = "پیش‌فرض", Color = "#333", Concurrent = 2, ScheduleType = "زمان" }
                };
            }
            lstGroups.ItemsSource = groups;
            lstGroups.SelectedIndex = 0;
        }
        private void ScheduleType_Checked(object sender, RoutedEventArgs e)
        {
            if (rbScheduleTime.IsChecked == true)
            {
                panelTime.Visibility = Visibility.Visible;
                panelWeekly.Visibility = Visibility.Collapsed;
                panelDateTime.Visibility = Visibility.Collapsed;
            }
            else if (rbScheduleWeekly.IsChecked == true)
            {
                panelTime.Visibility = Visibility.Collapsed;
                panelWeekly.Visibility = Visibility.Visible;
                panelDateTime.Visibility = Visibility.Collapsed;
            }
            else if (rbScheduleDateTime.IsChecked == true)
            {
                panelTime.Visibility = Visibility.Collapsed;
                panelWeekly.Visibility = Visibility.Collapsed;
                panelDateTime.Visibility = Visibility.Visible;
            }
        }
        private void LstGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group)
            {
                txtGroupColor.SelectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(group.Color);
                numGroupConcurrent.Value = group.Concurrent;
                txtGroupName.Text = group.Name;
                // Disable name editing for default group
                txtGroupName.IsEnabled = group.Name != "پیش‌فرض";
                // Disable settings panel for default group
                panelGroupSettings.IsEnabled = group.Name != "پیش‌فرض";
                // Schedule type
                if (group.ScheduleType == "زمان") rbScheduleTime.IsChecked = true;
                else if (group.ScheduleType == "هفتگی") rbScheduleWeekly.IsChecked = true;
                else if (group.ScheduleType == "تاریخ و زمان") rbScheduleDateTime.IsChecked = true;
                // Schedule values
                txtTime.Text = group.ScheduleTime;
                txtWeeklyTime.Text = group.WeeklyTime;
                dpDate.SelectedDate = group.ScheduleDate;
                txtDateTimeTime.Text = group.ScheduleDateTimeTime;
                // Set weekly days toggle state
                if (panelWeekly.Visibility == Visibility.Visible || group.WeeklyDays != null)
                {
                    var stackPanel = (StackPanel)panelWeekly.Children[1];
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is ToggleButton btn)
                        {
                            var dayName = btn.Content?.ToString();
                            btn.IsChecked = group.WeeklyDays.Contains(dayName);
                        }
                    }
                }
                lstGroupDownloads.ItemsSource = group.Downloads;
                ApplyScheduleUI(group);
            }
        }
        private void ApplyScheduleUI(DownloadGroup group)
        {
            if (group.ScheduleType == "زمان")
            {
                panelTime.Visibility = Visibility.Visible;
                panelWeekly.Visibility = Visibility.Collapsed;
                panelDateTime.Visibility = Visibility.Collapsed;
            }
            else if (group.ScheduleType == "هفتگی")
            {
                panelTime.Visibility = Visibility.Collapsed;
                panelWeekly.Visibility = Visibility.Visible;
                panelDateTime.Visibility = Visibility.Collapsed;
            }
            else if (group.ScheduleType == "تاریخ و زمان")
            {
                panelTime.Visibility = Visibility.Collapsed;
                panelWeekly.Visibility = Visibility.Collapsed;
                panelDateTime.Visibility = Visibility.Visible;
            }
        }
        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var input = CustomInputBox.ShowInputBox("نام گروه جدید را وارد کنید:", "افزودن گروه جدید");
            if (!string.IsNullOrWhiteSpace(input) && !groups.Any(g => g.Name == input))
            {
                var g = new DownloadGroup { Name = input };
                groups.Add(g);
                lstGroups.ItemsSource = null;
                lstGroups.ItemsSource = groups;
                lstGroups.SelectedItem = g;
                SaveGroups();
            }
        }
        private void BtnRenameGroup_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group)
            {
                var input = CustomInputBox.ShowInputBox("نام جدید گروه را وارد کنید:", "تغییر نام گروه", group.Name);
                if (!string.IsNullOrWhiteSpace(input) && !groups.Any(g => g.Name == input))
                {
                    group.Name = input;
                    lstGroups.ItemsSource = null;
                    lstGroups.ItemsSource = groups;
                    lstGroups.SelectedItem = group;
                    SaveGroups();
                }
            }
        }
        private void BtnDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group && group.Name != "پیش‌فرض")
            {
                var result = CustomMessageBox.ShowMessageBox($"آیا از حذف گروه '{group.Name}' مطمئن هستید؟", "حذف گروه", CustomMessageBoxType.YesNo);
                if (result == CustomMessageBoxResult.Yes)
                {
                    groups.Remove(group);
                    lstGroups.ItemsSource = null;
                    lstGroups.ItemsSource = groups;
                    lstGroups.SelectedIndex = 0;
                    SaveGroups();
                }
            }
        }
        private void BtnSaveGroupSettings_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group)
            {
                group.Color = txtGroupColor.SelectedColor.ToString();
                group.Concurrent = numGroupConcurrent.Value;
                group.Name = txtGroupName.Text;
                // Schedule type
                if (rbScheduleTime.IsChecked == true) group.ScheduleType = "زمان";
                else if (rbScheduleWeekly.IsChecked == true) group.ScheduleType = "هفتگی";
                else if (rbScheduleDateTime.IsChecked == true) group.ScheduleType = "تاریخ و زمان";
                // Schedule values
                group.ScheduleTime = txtTime.Text;
                group.WeeklyTime = txtWeeklyTime.Text;
                group.ScheduleDate = dpDate.SelectedDate;
                group.ScheduleDateTimeTime = txtDateTimeTime.Text;
                // Get weekly days toggle state
                if (panelWeekly.Visibility == Visibility.Visible)
                {
                    var days = new List<string>();
                    foreach (var child in ((StackPanel)panelWeekly.Children[1]).Children)
                    {
                        if (child is ToggleButton btn && btn.IsChecked == true)
                        {
                            var c = btn.Content.ToString()+ "";
                            days.Add(c);
                        }
                    }
                    group.WeeklyDays = days;
                }
                CustomMessageBox.ShowMessageBox("تنظیمات گروه ذخیره شد.", "ذخیره", CustomMessageBoxType.OK);
                SaveGroups();
            }
        }
        private void BtnUpPriority_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group && lstGroupDownloads.SelectedItem is DownloadItem item)
            {
                int idx = group.Downloads.IndexOf(item);
                if (idx > 0)
                {
                    group.Downloads.RemoveAt(idx);
                    group.Downloads.Insert(idx - 1, item);
                    lstGroupDownloads.ItemsSource = null;
                    lstGroupDownloads.ItemsSource = group.Downloads;
                    lstGroupDownloads.SelectedItem = item;
                    SaveGroups();
                }
            }
        }
        private void BtnDownPriority_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group && lstGroupDownloads.SelectedItem is DownloadItem item)
            {
                int idx = group.Downloads.IndexOf(item);
                if (idx < group.Downloads.Count - 1)
                {
                    group.Downloads.RemoveAt(idx);
                    group.Downloads.Insert(idx + 1, item);
                    lstGroupDownloads.ItemsSource = null;
                    lstGroupDownloads.ItemsSource = group.Downloads;
                    lstGroupDownloads.SelectedItem = item;
                    SaveGroups();
                }
            }
        }
        private void BtnRemoveDownload_Click(object sender, RoutedEventArgs e)
        {
            if (lstGroups.SelectedItem is DownloadGroup group && lstGroupDownloads.SelectedItem is DownloadItem item)
            {
                group.Downloads.Remove(item);
                lstGroupDownloads.ItemsSource = null;
                lstGroupDownloads.ItemsSource = group.Downloads;
                SaveGroups();
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void BtnStartQueue_Click(object sender, RoutedEventArgs e)
        {
            // شروع صف دانلود گروه
            if (lstGroups.SelectedItem is DownloadGroup activeGroup)
            {
                foreach (var download in activeGroup.Downloads.Where(d => d.Status == "Paused" || d.Status == "Stopped"))
                {
                    manager.StartDownload(download);
                }
                CustomMessageBox.ShowMessageBox($"شروع دانلودهای گروه '{activeGroup.Name}'", "شروع صف", CustomMessageBoxType.OK);
            }
        }
        
        private void BtnPauseQueue_Click(object sender, RoutedEventArgs e)
        {
            // توقف صف دانلود گروه
            if (lstGroups.SelectedItem is DownloadGroup activeGroup)
            {
                foreach (var download in activeGroup.Downloads.Where(d => d.Status == "Downloading"))
                {
                    manager.PauseDownload(download);
                }
                CustomMessageBox.ShowMessageBox($"توقف دانلودهای گروه '{activeGroup.Name}'", "توقف صف", CustomMessageBoxType.OK);
            }
        }
    }
} 