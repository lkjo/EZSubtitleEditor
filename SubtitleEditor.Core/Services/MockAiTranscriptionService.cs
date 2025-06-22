using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// 模擬的 AI 轉錄服務實作
    /// </summary>
    public class MockAiTranscriptionService : IAiTranscriptionService
    {
        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode)
        {
            // 委託給支援進度報告的版本，但不傳遞進度報告器
            return await TranscribeAsync(videoPath, language, generationMode, segmentationMode, null);
        }

        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode, IProgress<AiProgressInfo>? progressCallback)
        {
            // 模擬進度報告
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 0,
                StatusMessage = "模擬 AI 開始處理...",
                IsTranscriptionProgress = true,
                CurrentStep = "初始化",
                TotalSteps = 4,
                CurrentStepIndex = 1
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 25,
                StatusMessage = "模擬載入 AI 模型...",
                IsTranscriptionProgress = true,
                CurrentStep = "載入模型",
                TotalSteps = 4,
                CurrentStepIndex = 2
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 50,
                StatusMessage = "模擬語音轉錄...",
                IsTranscriptionProgress = true,
                CurrentStep = "語音轉錄",
                TotalSteps = 4,
                CurrentStepIndex = 3
            });

            await Task.Delay(2000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 75,
                StatusMessage = "模擬處理結果...",
                IsTranscriptionProgress = true,
                CurrentStep = "處理結果",
                TotalSteps = 4,
                CurrentStepIndex = 4
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 100,
                StatusMessage = "模擬轉錄完成！",
                IsTranscriptionProgress = true,
                CurrentStep = "完成",
                TotalSteps = 4,
                CurrentStepIndex = 4
            });

            // 回傳模擬的字幕資料
            return new List<SubtitleItem>
            {
                new SubtitleItem
                {
                    Index = 1,
                    StartTime = "00:00:00,0",
                    EndTime = "00:00:03,5",
                    Speaker = "AI",
                    Text = $"這是由 AI 生成的字幕 (語言: {language})"
                },
                new SubtitleItem
                {
                    Index = 2,
                    StartTime = "00:00:04,0",
                    EndTime = "00:00:07,2",
                    Speaker = "AI",
                    Text = $"使用模型: {generationMode}"
                },
                new SubtitleItem
                {
                    Index = 3,
                    StartTime = "00:00:08,0",
                    EndTime = "00:00:12,0",
                    Speaker = "AI",
                    Text = $"分段方式: {segmentationMode}"
                },
                new SubtitleItem
                {
                    Index = 4,
                    StartTime = "00:00:13,0",
                    EndTime = "00:00:16,5",
                    Speaker = "AI",
                    Text = "模擬 AI 轉錄完成！"
                }
            };
        }

        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language)
        {
            return await TranscribeInternalAsync(mediaFilePath, modelName, language, null);
        }

        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            return await TranscribeInternalAsync(mediaFilePath, modelName, language, progressCallback);
        }

        private async Task<IEnumerable<SubtitleItem>> TranscribeInternalAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            // 模擬進度報告
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 0,
                StatusMessage = $"模擬 AI 開始處理... (模型: {modelName})",
                IsTranscriptionProgress = true,
                CurrentStep = "初始化",
                TotalSteps = 4,
                CurrentStepIndex = 1
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 25,
                StatusMessage = $"模擬載入 {modelName} 模型...",
                IsTranscriptionProgress = true,
                CurrentStep = "載入模型",
                TotalSteps = 4,
                CurrentStepIndex = 2
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 50,
                StatusMessage = "模擬語音轉錄...",
                IsTranscriptionProgress = true,
                CurrentStep = "語音轉錄",
                TotalSteps = 4,
                CurrentStepIndex = 3
            });

            await Task.Delay(2000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 75,
                StatusMessage = "模擬處理結果...",
                IsTranscriptionProgress = true,
                CurrentStep = "處理結果",
                TotalSteps = 4,
                CurrentStepIndex = 4
            });

            await Task.Delay(1000);

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 100,
                StatusMessage = "模擬轉錄完成！",
                IsTranscriptionProgress = true,
                CurrentStep = "完成",
                TotalSteps = 4,
                CurrentStepIndex = 4
            });

            // 回傳模擬的字幕資料
            return new List<SubtitleItem>
            {
                new SubtitleItem
                {
                    Index = 1,
                    StartTime = "00:00:00,0",
                    EndTime = "00:00:03,5",
                    Speaker = "AI",
                    Text = $"這是由 AI 生成的字幕 (語言: {language})"
                },
                new SubtitleItem
                {
                    Index = 2,
                    StartTime = "00:00:04,0",
                    EndTime = "00:00:07,2",
                    Speaker = "AI",
                    Text = $"使用模型: {modelName}"
                },
                new SubtitleItem
                {
                    Index = 3,
                    StartTime = "00:00:08,0",
                    EndTime = "00:00:12,0",
                    Speaker = "AI",
                    Text = $"媒體檔案: {System.IO.Path.GetFileName(mediaFilePath)}"
                },
                new SubtitleItem
                {
                    Index = 4,
                    StartTime = "00:00:13,0",
                    EndTime = "00:00:16,5",
                    Speaker = "AI",
                    Text = "模擬 AI 轉錄完成！"
                }
            };
        }
    }
} 