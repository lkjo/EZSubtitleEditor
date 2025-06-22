using System;
using System.Globalization;
using System.Windows.Data;

namespace SubtitleEditor.Modules.Player.Views
{
    /// <summary>
    /// 將毫秒轉換為時間格式的轉換器
    /// </summary>
    public class TimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long milliseconds)
            {
                var timeSpan = TimeSpan.FromMilliseconds(milliseconds);
                return timeSpan.ToString(@"hh\:mm\:ss");
            }
            return "00:00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 