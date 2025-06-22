using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Events;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// AI 轉錄服務介面
    /// </summary>
    public interface IAiTranscriptionService
    {
        /// <summary>
        /// 非同步轉錄影片為字幕
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱 (例如 "base", "small", "medium", "large")</param>
        /// <param name="language">語言設定，預設為自動偵測</param>
        /// <returns>字幕項目列表</returns>
        Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language = "auto");

        /// <summary>
        /// 非同步轉錄影片為字幕，支援進度報告
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱 (例如 "base", "small", "medium", "large")</param>
        /// <param name="language">語言設定，預設為自動偵測</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language = "auto", IProgress<AiProgressInfo>? progressCallback = null);

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕
        /// </summary>
        /// <param name="videoPath">影片檔案路徑</param>
        /// <param name="language">語言設定</param>
        /// <param name="generationMode">文字生成方式</param>
        /// <param name="segmentationMode">文字分段方式</param>
        /// <returns>字幕項目列表</returns>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language) 方法")]
        Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode);

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕，支援進度報告
        /// </summary>
        /// <param name="videoPath">影片檔案路徑</param>
        /// <param name="language">語言設定</param>
        /// <param name="generationMode">文字生成方式</param>
        /// <param name="segmentationMode">文字分段方式</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>) 方法")]
        Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode, IProgress<AiProgressInfo>? progressCallback);
    }
} 