using Prism.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 字幕新增事件
    /// </summary>
    public class SubtitleAddedEvent : PubSubEvent<SubtitleItem>
    {
    }
} 