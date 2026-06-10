using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DownloadManagerH.Controls
{
    public partial class ColorDialogWindow : Window
    {
        private Color selectedColor;

        public Color SelectedColor => selectedColor;

        public ColorDialogWindow(Color initialColor)
        {
            InitializeComponent();
            selectedColor = initialColor;
            UpdateFromColor(initialColor);
        }

        private void UpdateFromColor(Color color)
        {
            sliderRed.Value = color.R;
            sliderGreen.Value = color.G;
            sliderBlue.Value = color.B;

            txtRed.Text = color.R.ToString();
            txtGreen.Text = color.G.ToString();
            txtBlue.Text = color.B.ToString();

            previewBrush.Color = color;
            txtColorHex.Text = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void Sliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == sliderRed)
                txtRed.Text = ((int)sliderRed.Value).ToString();
            else if (sender == sliderGreen)
                txtGreen.Text = ((int)sliderGreen.Value).ToString();
            else if (sender == sliderBlue)
                txtBlue.Text = ((int)sliderBlue.Value).ToString();

            UpdateColor();
        }

        private void UpdateColor()
        {
            try
            {
                byte r = byte.Parse(txtRed.Text);
                byte g = byte.Parse(txtGreen.Text);
                byte b = byte.Parse(txtBlue.Text);

                selectedColor = Color.FromRgb(r, g, b);
                previewBrush.Color = selectedColor;
                txtColorHex.Text = $"#FF{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                // در صورت خطا، مقدار فعلی حفظ می‌شود
            }
        }

        private void PresetColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Background is SolidColorBrush brush)
            {
                UpdateFromColor(brush.Color);
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtColorHex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryParseHexColor();
                e.Handled = true;
            }
        }

        private void TxtColorHex_KeyUp(object sender, KeyEventArgs e)
        {
            TryParseHexColor();
        }

        private void TryParseHexColor()
        {
            string hex = txtColorHex.Text.Trim();
            
            if (string.IsNullOrEmpty(hex))
                return;

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            Color? parsedColor = null;

            try
            {
                if (hex.Length == 7) // #RRGGBB
                {
                    byte r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                    parsedColor = Color.FromRgb(r, g, b);
                }
                else if (hex.Length == 9) // #AARRGGBB
                {
                    byte a = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                    byte r = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
                    parsedColor = Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                return;
            }

            if (parsedColor.HasValue)
            {
                UpdateFromColor(parsedColor.Value);
            }
        }
    }
}
