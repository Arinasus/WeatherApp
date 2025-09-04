using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WeatherApp.Pages
{
    public class PriorityToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string fileName = "belarus-gray.png";
            if (value is NewsPriority priority)
            {
                switch (priority)
                {
                    case NewsPriority.Critical:
                        fileName = "belarus-green.png";
                        break;
                    case NewsPriority.High:
                        fileName = "belarus-yellow.png";
                        break;
                    case NewsPriority.Normal:
                        fileName = "belarus-blue.png";
                        break;
                    case NewsPriority.Low:
                        fileName = "belarus-gray.png";
                        break;
                }
            }
            string absolutePath = $"C:/Users/HP/source/repos/WeatherApp/Images/{fileName}";

            try
            {
                return new BitmapImage(new Uri(absolutePath, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                // Выводим ошибку для отладки
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");

                // Подстраховка: вернуть серую иконку
                return new BitmapImage(new Uri(fallbackPath, UriKind.Absolute));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

