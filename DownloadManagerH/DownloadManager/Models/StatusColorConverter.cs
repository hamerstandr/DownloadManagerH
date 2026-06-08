using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DownloadManagerH.Models
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrWhiteSpace(status))
                return new SolidColorBrush(Colors.Gray);
            switch (status)
            {
                case "در انتظار": return new SolidColorBrush(Color.FromRgb(120, 144, 156)); // خاکستری
                case "در حال دانلود": return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // آبی
                case "متوقف موقت": return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // زرد
                case "تکمیل شده": return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // سبز
                case "ناموفق": return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // قرمز
                case "متوقف شده": return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // خاکستری روشن
                default: return new SolidColorBrush(Colors.Gray);
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 