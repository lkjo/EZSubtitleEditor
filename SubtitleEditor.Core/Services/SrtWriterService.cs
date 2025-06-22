using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// SRT 格式字幕寫入服務
    /// </summary>
    public class SrtWriterService : ISubtitleWriterService
    {
        /// <summary>
        /// 將字幕項目寫入到 SRT 格式檔案
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="subtitles">字幕項目集合</param>
        public void Write(string filePath, IEnumerable<SubtitleItem> subtitles)
        {
            Write(filePath, subtitles, false);
        }

        /// <summary>
        /// 將字幕項目寫入到 SRT 格式檔案
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="subtitles">字幕項目集合</param>
        /// <param name="includeSpeaker">是否包含演講者</param>
        public void Write(string filePath, IEnumerable<SubtitleItem> subtitles, bool includeSpeaker)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("檔案路徑不能為空", nameof(filePath));

            if (subtitles == null)
                throw new ArgumentNullException(nameof(subtitles));

            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    foreach (var subtitle in subtitles)
                    {
                        // 寫入序號
                        writer.WriteLine(subtitle.Index);

                        // 寫入時間軸格式：00:00:00,000 --> 00:00:05,000
                        writer.WriteLine($"{subtitle.StartTime} --> {subtitle.EndTime}");

                        // 寫入字幕文字（根據選項決定是否包含演講者）
                        if (includeSpeaker && !string.IsNullOrWhiteSpace(subtitle.Speaker))
                        {
                            writer.WriteLine($"{subtitle.Speaker}: {subtitle.Text}");
                        }
                        else
                        {
                            writer.WriteLine(subtitle.Text);
                        }

                        // 空行分隔
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"寫入 SRT 檔案時發生錯誤：{ex.Message}", ex);
            }
        }


    }
} 