using Prism.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 字幕準備就緒事件，傳遞 SubtitleProject 物件
    /// 可用於「從檔案開啟」和「從 AI 生成」
    /// </summary>
    public class SubtitlesReadyEvent : PubSubEvent<SubtitleProject>
    {
    }
} 