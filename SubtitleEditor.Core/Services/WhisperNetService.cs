using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using Whisper.net;
using FFMpegCore;
using FFMpegCore.Enums;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// 基於 Whisper.net 的 AI 轉錄服務，支援模型自動下載與快取
    /// 支援多種媒體格式自動轉換
    /// </summary>
    public class WhisperNetService : IAiTranscriptionService, IDisposable
    {
        private readonly string _modelsDirectory;
        private readonly string _tempDirectory;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _modelUrls;
        private readonly Dictionary<string, string> _languageMap;
        private readonly Dictionary<string, double> _modelProcessingSpeedFactors;

        public WhisperNetService()
        {
            // 設定模型快取目錄
            _modelsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubtitleEditor", "Models");
            
            // 設定暫時檔案目錄
            _tempDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubtitleEditor", "Temp");
            
            // 初始化 HttpClient
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30); // 設定30分鐘超時，適合大模型下載

            // 定義模型下載 URL (Hugging Face)
            _modelUrls = new Dictionary<string, string>
            {
                { "tiny", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin" },
                { "tiny.en", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin" },
                { "base", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" },
                { "base.en", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin" },
                { "small", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin" },
                { "small.en", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin" },
                { "medium", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin" },
                { "medium.en", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.en.bin" },
                { "large", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v1.bin" },
                { "large-v1", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v1.bin" },
                { "large-v2", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v2.bin" },
                { "large-v3", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin" }
            };

            // 語言對照表
            _languageMap = new Dictionary<string, string>
            {
                { "繁體中文", "zh" },
                { "简体中文", "zh" },
                { "English", "en" },
                { "auto", "auto" },
                { "日本語", "ja" },
                { "한국어", "ko" },
                { "Français", "fr" },
                { "Deutsch", "de" },
                { "Español", "es" },
                { "Português", "pt" },
                { "Русский", "ru" },
                { "العربية", "ar" },
                { "हिन्दी", "hi" },
                { "Italiano", "it" },
                { "Nederlands", "nl" },
                { "Polski", "pl" },
                { "Türkçe", "tr" },
                { "Svenska", "sv" },
                { "Norsk", "no" },
                { "Dansk", "da" }
            };

            // 模型處理速度係數（實時倍率，基於經驗值）
            // 例如：tiny 模型處理 1 分鐘音訊約需 0.2 分鐘
            _modelProcessingSpeedFactors = new Dictionary<string, double>
            {
                { "tiny", 0.15 },      // 最快：約 0.15x 實時速度
                { "base", 0.25 },      // 較快：約 0.25x 實時速度
                { "small", 0.4 },      // 中等：約 0.4x 實時速度
                { "medium", 0.6 },     // 較慢：約 0.6x 實時速度
                { "large", 1.0 },      // 最慢：約 1.0x 實時速度
                { "large-v1", 1.0 },
                { "large-v2", 1.0 },
                { "large-v3", 1.2 }    // 最新版本稍慢：約 1.2x 實時速度
            };

            // 確保模型目錄存在
            Directory.CreateDirectory(_modelsDirectory);
            Directory.CreateDirectory(_tempDirectory);
        }

        /// <summary>
        /// 非同步轉錄媒體檔案為字幕
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱</param>
        /// <param name="language">語言設定</param>
        /// <returns>字幕項目列表</returns>
        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language)
        {
            return await TranscribeWithProgressAsync(mediaFilePath, modelName, language, null);
        }

        /// <summary>
        /// 非同步轉錄媒體檔案為字幕，支援進度報告
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            return await TranscribeWithProgressAsync(mediaFilePath, modelName, language, progressCallback);
        }

        /// <summary>
        /// 內部轉錄實作方法
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        private async Task<IEnumerable<SubtitleItem>> TranscribeWithProgressAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            string? tempWavPath = null;
            DateTime startTime = DateTime.Now;
            
            try
            {
                // 步驟 1: 驗證輸入
                ValidateInputFile(mediaFilePath);
                
                // 獲取媒體時長和預估處理時間
                var (mediaDuration, estimatedTime) = await GetMediaDurationAndEstimateTimeAsync(mediaFilePath, modelName);
                
                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 0,
                    StatusMessage = $"準備轉錄環境... (預估需要 {estimatedTime})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "初始化",
                    TotalSteps = 5,
                    CurrentStepIndex = 1
                });

                // 步驟 2: 轉換媒體格式為 WAV（如果需要）
                var audioFilePath = await PrepareAudioFileAsync(mediaFilePath, progressCallback, estimatedTime);
                if (audioFilePath != mediaFilePath)
                {
                    tempWavPath = audioFilePath; // 記錄暫時檔案路徑，以便後續清理
                }

                // 步驟 3: 確保模型存在（下載如果需要）
                var localModelPath = await EnsureModelExistsAsync(modelName, progressCallback, estimatedTime);

                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 50,
                    StatusMessage = $"載入 AI 模型... (預估需要 {estimatedTime})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "載入模型",
                    TotalSteps = 5,
                    CurrentStepIndex = 4
                });

                // 步驟 4: 使用 Whisper.net 進行轉錄
                using var whisperFactory = WhisperFactory.FromPath(localModelPath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage(MapLanguageCode(language))
                    .Build();

                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 70,
                    StatusMessage = $"開始語音轉錄... (預估需要 {estimatedTime})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "語音轉錄",
                    TotalSteps = 5,
                    CurrentStepIndex = 5
                });

                // 執行轉錄
                var results = new List<SubtitleItem>();
                using var fileStream = File.OpenRead(audioFilePath);
                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        results.Add(new SubtitleItem
                        {
                            Index = results.Count + 1,
                            StartTime = FormatTimeSpan(segment.Start),
                            EndTime = FormatTimeSpan(segment.End),
                            Speaker = "Speaker1", // 預設演講者
                            Text = segment.Text.Trim()
                        });
                    }
                }

                var actualTime = DateTime.Now - startTime;
                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 100,
                    StatusMessage = $"轉錄完成！(實際用時 {FormatDuration(actualTime)})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "完成",
                    TotalSteps = 5,
                    CurrentStepIndex = 5
                });

                return results;
                }
                catch (Exception ex)
            {
                throw new InvalidOperationException($"Whisper 轉錄過程中發生錯誤：\n\n錯誤類型：{ex.GetType().Name}\n錯誤訊息：{ex.Message}", ex);
            }
            finally
            {
                // 清理暫時檔案
                if (!string.IsNullOrEmpty(tempWavPath) && File.Exists(tempWavPath))
                {
                    try
                    {
                        File.Delete(tempWavPath);
                    }
                    catch
                    {
                        // 忽略清理錯誤
                    }
                }
            }
        }

        /// <summary>
        /// 獲取媒體時長並計算預估處理時間
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱</param>
        /// <returns>媒體時長和預估處理時間字串</returns>
        private async Task<(TimeSpan duration, string estimatedTime)> GetMediaDurationAndEstimateTimeAsync(string mediaFilePath, string modelName)
        {
            try
            {
                // 使用 FFProbe 獲取媒體資訊
                var mediaInfo = await FFProbe.AnalyseAsync(mediaFilePath);
                var duration = mediaInfo.Duration;
                
                // 標準化模型名稱
                var normalizedModelName = NormalizeModelName(modelName);
                
                // 獲取模型處理速度係數
                var speedFactor = _modelProcessingSpeedFactors.TryGetValue(normalizedModelName, out var factor) ? factor : 0.5;
                
                // 計算預估時間：媒體時長 * 速度係數 * 1.3 (保險係數)
                var estimatedProcessingTime = TimeSpan.FromSeconds(duration.TotalSeconds * speedFactor * 1.3);
                
                // 最小 30 秒，最大 2 小時
                if (estimatedProcessingTime.TotalSeconds < 30)
                    estimatedProcessingTime = TimeSpan.FromSeconds(30);
                else if (estimatedProcessingTime.TotalHours > 2)
                    estimatedProcessingTime = TimeSpan.FromHours(2);
                
                return (duration, FormatDuration(estimatedProcessingTime));
            }
            catch
            {
                // 如果無法獲取媒體資訊，使用預設值
                return (TimeSpan.FromMinutes(5), "約 2-3 分鐘");
            }
        }

        /// <summary>
        /// 格式化時間長度為友善的字串
        /// </summary>
        /// <param name="duration">時間長度</param>
        /// <returns>格式化的時間字串</returns>
        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
                return $"約 {Math.Ceiling(duration.TotalSeconds)} 秒";
            else if (duration.TotalMinutes < 60)
                return $"約 {Math.Ceiling(duration.TotalMinutes)} 分鐘";
            else
                return $"約 {duration.Hours} 小時 {Math.Ceiling(duration.Minutes / 10.0) * 10} 分鐘";
        }

        /// <summary>
        /// 根據已過時間和進度計算剩餘時間（已停用，保留初始預估）
        /// </summary>
        /// <param name="startTime">開始時間</param>
        /// <param name="estimatedTotalTime">預估總時間字串</param>
        /// <param name="progress">當前進度 (0.0-1.0)</param>
        /// <returns>剩餘時間字串</returns>
        [System.Obsolete("已改為使用固定的初始預估時間")]
        private static string GetRemainingTimeString(DateTime startTime, string estimatedTotalTime, double progress)
        {
            if (progress <= 0) return estimatedTotalTime;
            
            var elapsed = DateTime.Now - startTime;
            var estimatedTotal = elapsed.TotalSeconds / progress;
            var remaining = TimeSpan.FromSeconds(estimatedTotal - elapsed.TotalSeconds);
            
            if (remaining.TotalSeconds <= 0)
                return "即將完成";
            
            return FormatDuration(remaining);
        }

        /// <summary>
        /// 準備音訊檔案，自動轉換為 WAV 格式（如果需要）
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <param name="estimatedTime">預估總時間</param>
        /// <returns>可用於 Whisper 的音訊檔案路徑</returns>
        private async Task<string> PrepareAudioFileAsync(string mediaFilePath, IProgress<AiProgressInfo>? progressCallback, string estimatedTime)
        {
            var extension = Path.GetExtension(mediaFilePath).ToLowerInvariant();
            
            // 如果已經是 WAV 格式，先嘗試直接使用
            if (extension == ".wav")
            {
                try
                {
                    // 嘗試讀取檔案頭驗證格式
                    using var fs = File.OpenRead(mediaFilePath);
                    var header = new byte[12];
                    await fs.ReadAsync(header, 0, 12);
                    
                    // 檢查 RIFF 和 WAVE 標頭
                    if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                        header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E')
                    {
                        progressCallback?.Report(new AiProgressInfo
                        {
                            ProgressPercentage = 15,
                            StatusMessage = $"使用原始 WAV 檔案 (預估需要 {estimatedTime})",
                            IsTranscriptionProgress = true,
                            CurrentStep = "音訊準備",
                            TotalSteps = 5,
                            CurrentStepIndex = 2
                        });
                        return mediaFilePath;
                    }
                }
                catch
                {
                    // 如果檢查失敗，繼續轉換流程
                }
            }

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 10,
                StatusMessage = $"轉換 {extension} 格式為 WAV... (預估需要 {estimatedTime})",
                IsTranscriptionProgress = true,
                CurrentStep = "音訊轉換",
                TotalSteps = 5,
                CurrentStepIndex = 2
            });

            // 生成暫時檔案路徑
            var tempFileName = $"whisper_temp_{Guid.NewGuid():N}.wav";
            var tempWavPath = Path.Combine(_tempDirectory, tempFileName);

            try
            {
                // 使用 FFMpeg 轉換為 WAV 格式
                // 設定為 16-bit PCM, 16kHz 單聲道，適合 Whisper 處理
                await FFMpegArguments
                    .FromFileInput(mediaFilePath)
                    .OutputToFile(tempWavPath, true, options => options
                        .WithCustomArgument("-acodec pcm_s16le") // 16-bit PCM Little Endian
                        .WithCustomArgument("-ar 16000") // 16kHz 採樣率
                        .WithCustomArgument("-ac 1") // 單聲道
                        .WithCustomArgument("-f wav") // WAV 格式
                    )
                    .ProcessAsynchronously();

                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 25,
                    StatusMessage = $"音訊格式轉換完成 (預估需要 {estimatedTime})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "音訊準備",
                    TotalSteps = 5,
                    CurrentStepIndex = 2
                });

                return tempWavPath;
            }
            catch (Exception ex)
            {
                // 清理失敗的暫時檔案
                if (File.Exists(tempWavPath))
                {
                    try { File.Delete(tempWavPath); } catch { }
                }

                throw new InvalidOperationException($"音訊格式轉換失敗：{ex.Message}\n\n" +
                    "可能的原因：\n" +
                    "1. 缺少 FFmpeg - 請安裝 FFmpeg 並確保已加入 PATH 環境變數\n" +
                    "2. 媒體檔案格式不支援或檔案損壞\n" +
                    "3. 磁碟空間不足\n\n" +
                    "支援的格式：MP4, AVI, MKV, MOV, MP3, AAC, FLAC, M4A, WAV", ex);
            }
        }

        /// <summary>
        /// 確保模型檔案存在，如果不存在則下載
        /// </summary>
        /// <param name="modelName">模型名稱</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <param name="estimatedTime">預估總時間</param>
        /// <returns>本地模型檔案路徑</returns>
        private async Task<string> EnsureModelExistsAsync(string modelName, IProgress<AiProgressInfo>? progressCallback, string estimatedTime)
        {
            // 標準化模型名稱
            var normalizedModelName = NormalizeModelName(modelName);
            var localModelPath = Path.Combine(_modelsDirectory, $"ggml-{normalizedModelName}.bin");

            // 檢查本地模型是否存在
            if (File.Exists(localModelPath))
            {
                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 30,
                    StatusMessage = $"使用快取的 {normalizedModelName} 模型 (預估需要 {estimatedTime})",
                    IsTranscriptionProgress = true,
                    CurrentStep = "模型檢查",
                    TotalSteps = 5,
                    CurrentStepIndex = 3
                });
                return localModelPath;
            }

            // 模型不存在，需要下載
            if (!_modelUrls.TryGetValue(normalizedModelName, out var downloadUrl))
            {
                throw new ArgumentException($"不支援的模型名稱：{modelName}。支援的模型：{string.Join(", ", _modelUrls.Keys)}");
            }

            // 觸發「開始下載」UI 提示
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 0,
                StatusMessage = $"開始下載 {normalizedModelName} 模型... (下載後預估處理 {estimatedTime})",
                IsDownloadProgress = true,
                CurrentStep = "下載模型",
                TotalSteps = 5,
                CurrentStepIndex = 3
            });

            // 下載模型
            await DownloadModelAsync(downloadUrl, localModelPath, progressCallback, estimatedTime);

            // 觸發「下載完成」UI 提示
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 45,
                StatusMessage = $"{normalizedModelName} 模型下載完成 (預估需要 {estimatedTime})",
                IsDownloadProgress = false,
                IsTranscriptionProgress = true,
                CurrentStep = "模型準備完成",
                TotalSteps = 5,
                CurrentStepIndex = 3
            });

            return localModelPath;
        }

        /// <summary>
        /// 下載模型檔案
        /// </summary>
        /// <param name="downloadUrl">下載 URL</param>
        /// <param name="localPath">本地儲存路徑</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <param name="estimatedTime">預估總處理時間</param>
        private async Task DownloadModelAsync(string downloadUrl, string localPath, IProgress<AiProgressInfo>? progressCallback, string estimatedTime)
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0 && progressCallback != null)
                {
                    var progressPercentage = (int)((downloadedBytes * 30) / totalBytes); // 下載佔 30% 進度
                    progressCallback.Report(new AiProgressInfo
                    {
                        ProgressPercentage = progressPercentage,
                        StatusMessage = $"下載中... {downloadedBytes / (1024 * 1024):F1} MB / {totalBytes / (1024 * 1024):F1} MB (下載後預估處理 {estimatedTime})",
                        IsDownloadProgress = true,
                        CurrentStep = "下載模型",
                        TotalSteps = 5,
                        CurrentStepIndex = 3
                    });
                }
            }
        }

        /// <summary>
        /// 標準化模型名稱
        /// </summary>
        /// <param name="modelName">輸入的模型名稱</param>
        /// <returns>標準化的模型名稱</returns>
        private static string NormalizeModelName(string modelName)
        {
            return modelName?.ToLowerInvariant() switch
            {
                "輕型模型 (tiny)" or "tiny" => "tiny",
                "小型模型 (base)" or "base" => "base",
                "中型模型 (medium)" or "medium" => "medium",
                "大型模型 (large)" or "large" => "large-v3",
                _ => modelName?.ToLowerInvariant() ?? "base"
            };
        }

        /// <summary>
        /// 將語言名稱對應到語言代碼
        /// </summary>
        /// <param name="language">語言名稱</param>
        /// <returns>語言代碼</returns>
        private string MapLanguageCode(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "auto";

            return _languageMap.TryGetValue(language, out var code) ? code : language;
        }

        /// <summary>
        /// 驗證輸入檔案
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        private static void ValidateInputFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("檔案路徑不能為空");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到檔案：{filePath}");

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
                throw new InvalidOperationException("檔案大小為 0，可能是損壞的檔案");
        }

        /// <summary>
        /// 格式化 TimeSpan 為 SRT 格式的時間字串
        /// </summary>
        /// <param name="timeSpan">時間間隔</param>
        /// <returns>SRT 格式的時間字串</returns>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{timeSpan.Milliseconds:D3}";
        }

        /// <summary>
        /// 釋放資源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #region 舊版本相容方法

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕
        /// </summary>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language) 方法")]
        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode)
        {
            var modelName = MapGenerationModeToModelName(generationMode);
            var results = await TranscribeAsync(videoPath, modelName, language);
            return results.ToList();
        }

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕，支援進度報告
        /// </summary>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>) 方法")]
        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode, IProgress<AiProgressInfo>? progressCallback)
        {
            var modelName = MapGenerationModeToModelName(generationMode);
            var results = await TranscribeWithProgressAsync(videoPath, modelName, MapLanguageCode(language), progressCallback);
            return results.ToList();
        }

        /// <summary>
        /// 將舊的生成模式對應到新的模型名稱
        /// </summary>
        /// <param name="generationMode">生成模式</param>
        /// <returns>模型名稱</returns>
        private static string MapGenerationModeToModelName(string generationMode)
        {
            return generationMode switch
            {
                "輕型模型 (tiny)" => "tiny",
                "小型模型 (base)" => "base",
                "中型模型 (medium)" => "medium",
                "大型模型 (large)" => "large-v3",
                _ => "base"
            };
        }

        #endregion
    }
} 