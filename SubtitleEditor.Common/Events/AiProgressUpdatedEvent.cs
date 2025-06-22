using Prism.Events;

namespace SubtitleEditor.Common.Events
{
    /// <summary>
    /// AI 處理進度更新事件
    /// </summary>
    public class AiProgressUpdatedEvent : PubSubEvent<AiProgressInfo>
    {
    }

    /// <summary>
    /// AI 進度資訊
    /// </summary>
    public class AiProgressInfo
    {
        /// <summary>
        /// 進度百分比 (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// 進度描述文字
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// 是否為下載進度
        /// </summary>
        public bool IsDownloadProgress { get; set; }

        /// <summary>
        /// 是否為轉錄進度
        /// </summary>
        public bool IsTranscriptionProgress { get; set; }

        /// <summary>
        /// 當前步驟
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// 總步驟數
        /// </summary>
        public int TotalSteps { get; set; }

        /// <summary>
        /// 當前步驟索引
        /// </summary>
        public int CurrentStepIndex { get; set; }
    }
} 