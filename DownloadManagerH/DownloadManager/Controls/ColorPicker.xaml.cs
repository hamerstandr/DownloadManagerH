using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DownloadManagerH.Controls
{
    public partial class ColorPicker : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorPicker),
                new PropertyMetadata(Colors.Blue, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get { return (Color)GetValue(SelectedColorProperty); }
            set { SetValue(SelectedColorProperty, value); }
        }

        public ColorPicker()
        {
            InitializeComponent();
            UpdatePreview();
        }

        private void BtnSelectColor_Click(object sender, RoutedEventArgs e)
        {
            // ایجاد دیالوگ انتخاب رنگ ساده
            var colorDialog = new ColorDialogWindow(SelectedColor);
            if (colorDialog.ShowDialog() == true)
            {
                SelectedColor = colorDialog.SelectedColor;
            }
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker picker)
            {
                picker.UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            brushPreview.Color = SelectedColor;
            txtHexColor.Text = SelectedColor.ToString();
        }

        public string GetHexColor()
        {
            return $"#{SelectedColor.A:X2}{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        }

        public void SetHexColor(string hexColor)
        {
            try
            {
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }

                if (hexColor.Length == 6)
                {
                    byte a = 255;
                    byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                    SelectedColor = Color.FromArgb(a, r, g, b);
                }
                else if (hexColor.Length == 8)
                {
                    byte a = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte r = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                    SelectedColor = Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // در صورت خطا، رنگ پیش‌فرض حفظ می‌شود
            }
        }
    }
}
