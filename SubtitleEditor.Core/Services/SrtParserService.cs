using System.Text.RegularExpressions;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// SRT 字幕檔案解析服務
    /// </summary>
    public class SrtParserService : ISubtitleParserService
    {
        private readonly Regex _timeRegex = new Regex(@"(\d{2}:\d{2}:\d{2},\d+)\s*-->\s*(\d{2}:\d{2}:\d{2},\d+)", RegexOptions.Compiled);

        /// <summary>
        /// 解析 SRT 字幕檔案
        /// </summary>
        /// <param name="filePath">SRT 檔案路徑</param>
        /// <returns>解析後的字幕項目集合</returns>
        /// <exception cref="FileNotFoundException">檔案不存在</exception>
        /// <exception cref="ArgumentException">檔案格式不正確</exception>
        public IEnumerable<SubtitleItem> Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"字幕檔案不存在：{filePath}");
            }

            var subtitles = new List<SubtitleItem>();
            
            try
            {
                // 讀取檔案內容
                var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                
                // 分割字幕區塊 - 支援不同的換行符號
                var blocks = content.Split(new string[] { "\r\n\r\n", "\n\n", "\r\r" }, 
                                         StringSplitOptions.RemoveEmptyEntries);

                foreach (var block in blocks)
                {
                    var subtitle = ParseSubtitleBlock(block.Trim());
                    if (subtitle != null)
                    {
                        subtitles.Add(subtitle);
                    }
                }
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                throw new ArgumentException($"解析字幕檔案時發生錯誤：{ex.Message}", ex);
            }

            return subtitles;
        }

        /// <summary>
        /// 解析單一字幕區塊
        /// </summary>
        /// <param name="block">字幕區塊文字</param>
        /// <returns>解析後的字幕項目，解析失敗則回傳 null</returns>
        private SubtitleItem? ParseSubtitleBlock(string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return null;

            var lines = block.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length < 3)
                return null;

            try
            {
                // 第一行：序號
                if (!int.TryParse(lines[0].Trim(), out var index))
                    return null;

                // 第二行：時間範圍
                var timeMatch = _timeRegex.Match(lines[1]);
                if (!timeMatch.Success)
                    return null;

                var startTime = timeMatch.Groups[1].Value;
                var endTime = timeMatch.Groups[2].Value;

                // 第三行以後：字幕文字
                var textLines = lines.Skip(2).Where(line => !string.IsNullOrWhiteSpace(line));
                var fullText = string.Join("\n", textLines);

                // 解析演講者和字幕文字
                var speaker = "Speaker1";
                var text = fullText;

                // 檢查是否包含演講者格式（演講者名稱：字幕內容）
                var colonIndex = fullText.IndexOf(':');
                if (colonIndex > 0 && colonIndex < fullText.Length - 1)
                {
                    var potentialSpeaker = fullText.Substring(0, colonIndex).Trim();
                    // 如果冒號前的內容不超過20個字元且不包含換行，則視為演講者
                    if (potentialSpeaker.Length <= 20 && !potentialSpeaker.Contains('\n'))
                    {
                        speaker = potentialSpeaker;
                        text = fullText.Substring(colonIndex + 1).Trim();
                    }
                }

                return new SubtitleItem
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Speaker = speaker,
                    Text = text
                };
            }
            catch
            {
                // 解析失敗，忽略此區塊
                return null;
            }
        }
    }
} 