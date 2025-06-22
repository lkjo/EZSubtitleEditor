using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 影片長度變化事件，傳遞影片總長度（毫秒）
    /// </summary>
    public class VideoLengthChangedEvent : PubSubEvent<long>
    {
    }
} 