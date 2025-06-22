using Prism.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 字幕刪除事件
    /// </summary>
    public class SubtitleDeletedEvent : PubSubEvent<SubtitleItem>
    {
    }
} 