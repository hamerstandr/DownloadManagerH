using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace DownloadManagerH.Models
{
    public class GroupColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string color = value as string;
            if (string.IsNullOrWhiteSpace(color) || color == "پیش‌فرض")
                return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // آبی پیش‌فرض
            try
            {
                return (SolidColorBrush)(new BrushConverter().ConvertFrom(color));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(33, 150, 243));
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
