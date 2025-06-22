using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// 時間軸捲動事件
    /// 傳遞的 double 值為：目標時間點在畫布上的像素位置（而非 ScrollViewer 的捲動偏移量）
    /// </summary>
    public class ScrollTimelineEvent : PubSubEvent<double>
    {
    }
} 