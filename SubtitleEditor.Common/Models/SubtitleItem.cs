using Prism.Mvvm;
using System.Text.RegularExpressions;

namespace SubtitleEditor.Common.Models
{
    public class SubtitleItem : BindableBase
    {
        private static readonly Regex TimeFormatRegex = new Regex(@"^(\d{1,2}):(\d{1,2}):(\d{1,2}),(\d*)$", RegexOptions.Compiled);

        private int _index;
        public int Index
        {
            get { return _index; }
            set { SetProperty(ref _index, value); }
        }

        private string _startTime = "00:00:00,0";
        public string StartTime
        {
            get { return _startTime; }
            set { SetProperty(ref _startTime, NormalizeTimeFormat(value)); }
        }

        private string _endTime = "00:00:00,0";
        public string EndTime
        {
            get { return _endTime; }
            set { SetProperty(ref _endTime, NormalizeTimeFormat(value)); }
        }

        private string _speaker = "Speaker1";
        public string Speaker
        {
            get { return _speaker; }
            set { SetProperty(ref _speaker, value); }
        }

        private string _text = string.Empty;
        public string Text
        {
            get { return _text; }
            set { SetProperty(ref _text, value); }
        }

        private bool _isActive = false;
        public bool IsActive
        {
            get { return _isActive; }
            set { SetProperty(ref _isActive, value); }
        }

        /// <summary>
        /// 標準化時間格式，自動補齊不完整的時間
        /// </summary>
        /// <param name="timeString">使用者輸入的時間字串</param>
        /// <returns>標準化後的時間字串</returns>
        private static string NormalizeTimeFormat(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
                return "00:00:00,0";

            // 移除多餘的空白
            timeString = timeString.Trim();

            // 嘗試使用正規表達式解析
            var match = TimeFormatRegex.Match(timeString);
            if (match.Success)
            {
                var hours = int.Parse(match.Groups[1].Value).ToString("D2");
                var minutes = int.Parse(match.Groups[2].Value).ToString("D2");
                var seconds = int.Parse(match.Groups[3].Value).ToString("D2");
                var fraction = match.Groups[4].Value;

                // 如果小數部分為空或不完整，補齊為 0
                if (string.IsNullOrEmpty(fraction))
                {
                    fraction = "0";
                }
                else if (fraction.Length > 1)
                {
                    // 如果小數部分超過一位，只取第一位
                    fraction = fraction.Substring(0, 1);
                }

                return $"{hours}:{minutes}:{seconds},{fraction}";
            }

            // 嘗試處理一些常見的不完整格式
            timeString = timeString.Replace(" ", "");

            // 處理結尾逗號的情況（如 "00:00:09,"）
            if (timeString.EndsWith(","))
            {
                timeString += "0";
                return NormalizeTimeFormat(timeString);
            }

            // 處理只有時分秒沒有小數的情況（如 "00:00:09"）
            if (Regex.IsMatch(timeString, @"^\d{1,2}:\d{1,2}:\d{1,2}$"))
            {
                timeString += ",0";
                return NormalizeTimeFormat(timeString);
            }

            // 處理純數字格式
            if (Regex.IsMatch(timeString, @"^\d+$"))
            {
                return ParseNumericTimeFormat(timeString);
            }

            // 處理分:秒格式（如 "1:30" -> "00:01:30,0"）
            if (Regex.IsMatch(timeString, @"^\d{1,2}:\d{1,2}$"))
            {
                return $"00:{timeString},0";
            }

            // 如果都無法識別，返回預設值
            return "00:00:00,0";
        }

        /// <summary>
        /// 解析純數字時間格式
        /// </summary>
        /// <param name="numericString">純數字字串</param>
        /// <returns>標準化的時間字串</returns>
        private static string ParseNumericTimeFormat(string numericString)
        {
            // 移除開頭的零
            numericString = numericString.TrimStart('0');
            
            // 如果全是零或為空，返回預設值
            if (string.IsNullOrEmpty(numericString))
                return "00:00:00,0";

            // 補齊到偶數位數以便分組
            if (numericString.Length % 2 == 1)
                numericString = "0" + numericString;

            // 根據長度決定解析方式
            switch (numericString.Length)
            {
                case 2: // 如 "05" -> "00:00:05,0"
                    return $"00:00:{numericString},0";
                
                case 4: // 如 "1234" -> "00:12:34,0"
                    return $"00:{numericString.Substring(0, 2)}:{numericString.Substring(2, 2)},0";
                
                case 6: // 如 "123456" -> "12:34:56,0"
                    return $"{numericString.Substring(0, 2)}:{numericString.Substring(2, 2)}:{numericString.Substring(4, 2)},0";
                
                default:
                    // 超過6位數或其他情況，取最後6位作為時分秒
                    if (numericString.Length > 6)
                    {
                        var lastSix = numericString.Substring(numericString.Length - 6);
                        return $"{lastSix.Substring(0, 2)}:{lastSix.Substring(2, 2)}:{lastSix.Substring(4, 2)},0";
                    }
                    else
                    {
                        // 少於2位數的情況（理論上不會發生，因為我們已經補齊）
                        return "00:00:00,0";
                    }
            }
        }
    }
} 