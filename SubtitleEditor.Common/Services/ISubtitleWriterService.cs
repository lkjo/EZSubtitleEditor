using System.Collections.Generic;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// 字幕寫入服務介面
    /// </summary>
    public interface ISubtitleWriterService
    {
        /// <summary>
        /// 將字幕項目寫入到指定的檔案路徑
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="subtitles">字幕項目集合</param>
        void Write(string filePath, IEnumerable<SubtitleItem> subtitles);

        /// <summary>
        /// 將字幕項目寫入到指定的檔案路徑
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="subtitles">字幕項目集合</param>
        /// <param name="includeSpeaker">是否包含演講者</param>
        void Write(string filePath, IEnumerable<SubtitleItem> subtitles, bool includeSpeaker);
    }
} 