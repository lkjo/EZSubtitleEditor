using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 影片載入事件，傳遞載入的影片路徑
    /// </summary>
    public class VideoLoadedEvent : PubSubEvent<string>
    {
    }
} 