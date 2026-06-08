using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;
using DownloadManagerH.Models;

namespace DownloadManagerH.Windows.Dialog
{
    public partial class BulkAddDialog : Window
    {
        public ObservableCollection<BulkLinkItem> Links { get; set; }
        public string SelectedGroup => cmbGroup.SelectedItem?.ToString() ?? "پیش‌فرض";
        public string SavePath => txtSavePath.Text;
        public BulkAddDialog(System.Collections.Generic.IEnumerable<string> urls, System.Collections.Generic.IEnumerable<string> groups, string defaultPath)
        {
            InitializeComponent();
            Links = new ObservableCollection<BulkLinkItem>(urls.Select(u => new BulkLinkItem { Url = u, FileType = System.IO.Path.GetExtension(u), IsSelected = true }));
            dgLinks.ItemsSource = Links;
            cmbGroup.ItemsSource = groups;
            cmbGroup.SelectedIndex = 0;
            txtSavePath.Text = defaultPath;
        }
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => Links.ToList().ForEach(l => l.IsSelected = true);
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e) => Links.ToList().ForEach(l => l.IsSelected = false);
        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var input = CustomInputBox.ShowInputBox("نام گروه جدید را وارد کنید:", "افزودن گروه جدید");
            if (!string.IsNullOrWhiteSpace(input) && !cmbGroup.Items.Contains(input))
            {
                var list = cmbGroup.Items.Cast<string>().ToList();
                list.Add(input);
                cmbGroup.ItemsSource = null;
                cmbGroup.ItemsSource = list;
                cmbGroup.SelectedItem = input;
            }
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        public System.Collections.Generic.List<BulkLinkItem> GetSelectedLinks() => Links.Where(l => l.IsSelected).ToList();
    }
    public class BulkLinkItem
    {
        public bool IsSelected { get; set; }
        public string Url { get; set; } = "";
        public string FileType { get; set; } = "";
    }
} 