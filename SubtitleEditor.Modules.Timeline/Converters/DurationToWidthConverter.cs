using System;
using System.Globalization;
using System.Windows.Data;

namespace SubtitleEditor.Modules.Timeline.Converters
{
    /// <summary>
    /// 將持續時長轉換為像素寬度的轉換器
    /// </summary>
    public class DurationToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 || 
                values[0] == null || 
                values[1] == null || 
                values[2] == null || 
                values[3] == null)
                return 0.0;

            try
            {
                // values[0]: string (開始時間)
                // values[1]: string (結束時間)
                // values[2]: long (總時長，毫秒)
                // values[3]: double (總寬度，像素)
                
                var startTimeString = values[0].ToString();
                var endTimeString = values[1].ToString();
                
                TimeSpan startTime, endTime;
                
                // 嘗試多種時間格式解析開始時間
                if (!TimeSpan.TryParseExact(startTimeString, @"hh\:mm\:ss\,f", null, out startTime) &&
                    !TimeSpan.TryParseExact(startTimeString, @"hh\:mm\:ss\,ff", null, out startTime) &&
                    !TimeSpan.TryParseExact(startTimeString, @"hh\:mm\:ss\,fff", null, out startTime))
                {
                    System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter: 無法解析開始時間格式 '{startTimeString}'");
                    return 0.0;
                }
                
                // 嘗試多種時間格式解析結束時間
                if (!TimeSpan.TryParseExact(endTimeString, @"hh\:mm\:ss\,f", null, out endTime) &&
                    !TimeSpan.TryParseExact(endTimeString, @"hh\:mm\:ss\,ff", null, out endTime) &&
                    !TimeSpan.TryParseExact(endTimeString, @"hh\:mm\:ss\,fff", null, out endTime))
                {
                    System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter: 無法解析結束時間格式 '{endTimeString}'");
                    return 0.0;
                }

                var totalDurationMs = System.Convert.ToInt64(values[2]);
                var totalWidth = System.Convert.ToDouble(values[3]);

                if (totalDurationMs <= 0 || totalWidth <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter: 總時長或寬度無效 - 總時長: {totalDurationMs}ms, 寬度: {totalWidth}px");
                    return 0.0;
                }

                // 計算持續時長
                var durationMs = (endTime - startTime).TotalMilliseconds;
                if (durationMs <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter: 持續時長無效 - 開始: {startTimeString}, 結束: {endTimeString}");
                    return 0.0;
                }

                // 計算對應的像素寬度
                var pixelWidth = (durationMs / totalDurationMs) * totalWidth;

                System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter: {startTimeString}-{endTimeString} -> {pixelWidth}px (持續: {durationMs}ms)");
                return Math.Max(1, pixelWidth); // 最小寬度為 1 像素
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DurationToWidthConverter 錯誤: {ex.Message}");
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 