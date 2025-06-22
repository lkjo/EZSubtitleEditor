using System.Collections.Generic;

namespace SubtitleEditor.Common.Models
{
    /// <summary>
    /// 字幕專案資料模型，包含檔案路徑和字幕項目
    /// </summary>
    public record SubtitleProject(string FilePath, IEnumerable<SubtitleItem> Items);
} 