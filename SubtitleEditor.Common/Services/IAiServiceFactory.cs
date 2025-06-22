using SubtitleEditor.Common.Enums;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// AI 服務工廠介面
    /// </summary>
    public interface IAiServiceFactory
    {
        /// <summary>
        /// 根據服務類型取得對應的 AI 轉錄服務
        /// </summary>
        /// <param name="serviceType">服務類型</param>
        /// <returns>AI 轉錄服務實例</returns>
        IAiTranscriptionService GetService(AiServiceType serviceType);
    }
} 