using System.Windows;
using DownloadManagerH.Windows;

namespace DownloadManagerH.Windows.Dialog
{
    public partial class CustomInputBox : Window
    {
        public string InputText => txtInput.Text;
        public CustomInputBox(string prompt, string title = "ورودی")
        {
            InitializeComponent();
            this.Title = title;
            lblPrompt.Text = prompt;
            txtInput.Focus();
            txtInput.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    BtnOk_Click(s, e);
                }
            };
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        public static string Show(string prompt, string title = "ورودی", string defaultValue = "")
        {
            var win = new CustomInputBox(prompt, title)
            {
                Owner = App.Current.MainWindow
            };
            win.txtInput.Text = defaultValue;
            if (win.ShowDialog() == true)
                return win.InputText;
            return "";
        }
        public static string ShowInputBox(string prompt, string title = "ورودی", string defaultValue = "")
        {
            var win = new CustomInputBox(prompt, title)
            {
                Owner = App.Current.MainWindow
            };
            win.txtInput.Text = defaultValue;
            if (win.ShowDialog() == true)
                return win.InputText;
            return "";
        }
    }
} 