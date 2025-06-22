using Prism.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 選中字幕項目事件
    /// </summary>
    public class SelectSubtitleEvent : PubSubEvent<SubtitleItem>
    {
    }
} 