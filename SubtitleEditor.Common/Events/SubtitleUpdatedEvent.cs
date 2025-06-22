using Prism.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 字幕項目更新事件，傳遞已更新的字幕項目
    /// </summary>
    public class SubtitleUpdatedEvent : PubSubEvent<SubtitleItem>
    {
    }
} 