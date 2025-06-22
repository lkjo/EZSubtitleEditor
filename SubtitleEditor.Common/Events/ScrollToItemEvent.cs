using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 捲動到指定字幕項目的事件
    /// </summary>
    public class ScrollToItemEvent : PubSubEvent<Models.SubtitleItem>
    {
    }
} 