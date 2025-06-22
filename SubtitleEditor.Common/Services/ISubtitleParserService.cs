using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// 字幕解析服務介面
    /// </summary>
    public interface ISubtitleParserService
    {
        /// <summary>
        /// 解析指定路徑的字幕檔案
        /// </summary>
        /// <param name="filePath">字幕檔案路徑</param>
        /// <returns>解析後的字幕項目集合</returns>
        IEnumerable<SubtitleItem> Parse(string filePath);
    }
} 