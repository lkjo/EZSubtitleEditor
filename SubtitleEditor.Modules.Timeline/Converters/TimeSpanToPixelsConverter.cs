using System;
using System.Globalization;
using System.Windows.Data;

namespace SubtitleEditor.Modules.Timeline.Converters
{
    /// <summary>
    /// 將 TimeSpan 轉換為像素位置的轉換器
    /// </summary>
    public class TimeSpanToPixelsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || 
                values[0] == null || 
                values[1] == null || 
                values[2] == null)
                return 0.0;

            try
            {
                // values[0]: TimeSpan (字幕開始時間)
                // values[1]: long (總時長，毫秒)
                // values[2]: double (總寬度，像素)
                
                var timeString = values[0].ToString();
                TimeSpan startTime;
                
                // 嘗試多種時間格式
                if (!TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,f", null, out startTime) &&
                    !TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,ff", null, out startTime) &&
                    !TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,fff", null, out startTime))
                {
                    System.Diagnostics.Debug.WriteLine($"TimeSpanToPixelsConverter: 無法解析時間格式 '{timeString}'");
                    return 0.0;
                }

                var totalDurationMs = System.Convert.ToInt64(values[1]);
                var totalWidth = System.Convert.ToDouble(values[2]);

                if (totalDurationMs <= 0 || totalWidth <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"TimeSpanToPixelsConverter: 總時長或寬度無效 - 總時長: {totalDurationMs}ms, 寬度: {totalWidth}px");
                    return 0.0;
                }

                // 計算比例位置
                var startTimeMs = startTime.TotalMilliseconds;
                var pixelPosition = (startTimeMs / totalDurationMs) * totalWidth;

                System.Diagnostics.Debug.WriteLine($"TimeSpanToPixelsConverter: {timeString} -> {pixelPosition}px (總時長: {totalDurationMs}ms, 總寬度: {totalWidth}px)");
                return Math.Max(0, pixelPosition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimeSpanToPixelsConverter 錯誤: {ex.Message}");
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 