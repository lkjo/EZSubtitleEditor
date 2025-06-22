using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 儲存字幕事件
    /// </summary>
    public class SaveSubtitleEvent : PubSubEvent<string>
    {
        // 事件載荷為要儲存的檔案路徑
    }
} 