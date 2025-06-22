using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;
using Xabe.FFmpeg;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// 基於 OpenAI API 的雲端 AI 轉錄服務
    /// 使用 OpenAI 的 Whisper-1 模型進行語音轉錄
    /// 支援大檔案的精準分塊處理
    /// </summary>
    public class WhisperApiService : IAiTranscriptionService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private const string OpenAiApiUrl = "https://api.openai.com/v1/audio/transcriptions";
        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25MB OpenAI 限制
        private const long SafeFileSizeBytes = 24 * 1024 * 1024; // 24MB 安全限制
        private string? _temporaryApiKey; // 暫時的API Key

        public WhisperApiService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10); // 設定10分鐘超時
        }

        /// <summary>
        /// 設定暫時的API Key（用於當次轉錄）
        /// </summary>
        /// <param name="apiKey">API Key</param>
        public void SetTemporaryApiKey(string apiKey)
        {
            _temporaryApiKey = apiKey;
        }

        /// <summary>
        /// 非同步轉錄媒體檔案為字幕
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱（雲端服務固定使用 whisper-1）</param>
        /// <param name="language">語言設定</param>
        /// <returns>字幕項目列表</returns>
        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language)
        {
            return await TranscribeInternalAsync(mediaFilePath, modelName, language, null);
        }

        /// <summary>
        /// 非同步轉錄媒體檔案為字幕，支援進度報告
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱（雲端服務固定使用 whisper-1）</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        public async Task<IEnumerable<SubtitleItem>> TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            return await TranscribeInternalAsync(mediaFilePath, modelName, language, progressCallback);
        }

        /// <summary>
        /// 內部轉錄實作方法 - 重構版本，支援精準分塊處理
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="modelName">模型名稱</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>字幕項目列表</returns>
        private async Task<IEnumerable<SubtitleItem>> TranscribeInternalAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            var tempFiles = new List<string>();
            
            try
            {
                // 步驟 1: 驗證設定和檔案
                ValidateConfiguration();
                ValidateInputFile(mediaFilePath);

                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = 0,
                    StatusMessage = "準備雲端轉錄服務...",
                    IsTranscriptionProgress = true,
                    CurrentStep = "初始化",
                    TotalSteps = 7,
                    CurrentStepIndex = 1
                });

                // 步驟 2: 檢查檔案大小
                var fileInfo = new FileInfo(mediaFilePath);
                
                if (fileInfo.Length <= MaxFileSizeBytes)
                {
                    // 檔案大小符合限制，直接處理
                    progressCallback?.Report(new AiProgressInfo
                    {
                        ProgressPercentage = 10,
                        StatusMessage = $"檔案大小 ({fileInfo.Length / 1024 / 1024:F1} MB) 符合限制，直接處理...",
                        IsTranscriptionProgress = true,
                        CurrentStep = "直接處理",
                        TotalSteps = 7,
                        CurrentStepIndex = 2
                    });

                    var response = await CallOpenAiApiAsync(mediaFilePath, language, progressCallback);
                    var subtitles = ConvertResponseToSubtitles(response);
                    
                    progressCallback?.Report(new AiProgressInfo
                    {
                        ProgressPercentage = 100,
                        StatusMessage = $"轉錄完成！共生成 {subtitles.Count} 條字幕",
                        IsTranscriptionProgress = true,
                        CurrentStep = "完成",
                        TotalSteps = 7,
                        CurrentStepIndex = 7
                    });

                    return subtitles;
                }
                else
                {
                    // 檔案太大，執行精準分塊處理
                    return await ProcessLargeFileAsync(mediaFilePath, language, progressCallback, tempFiles);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"網路連線錯誤：{ex.Message}\n\n請檢查：\n1. 網路連線是否正常\n2. OpenAI API 服務是否可用\n3. API Key 是否有效", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException($"API 呼叫超時：{ex.Message}\n\n可能原因：\n1. 網路連線不穩定\n2. OpenAI 服務繁忙\n3. 檔案片段處理時間過長", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                throw new InvalidOperationException("API Key 無效或已過期。\n\n請檢查：\n1. API Key 是否正確設定\n2. API Key 是否仍然有效\n3. 帳戶是否有足夠的額度", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
            {
                throw new InvalidOperationException("API 呼叫頻率過高。\n\n請稍後再試，或檢查您的 API 使用額度。", ex);
            }
            finally
            {
                // 清理暫時檔案
                await CleanupTempFilesAsync(tempFiles);
            }
        }

        /// <summary>
        /// 處理大檔案的精準分塊邏輯
        /// </summary>
        /// <param name="mediaFilePath">原始媒體檔案路徑</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <param name="tempFiles">暫存檔案列表</param>
        /// <returns>字幕項目列表</returns>
        private async Task<IEnumerable<SubtitleItem>> ProcessLargeFileAsync(string mediaFilePath, string language, IProgress<AiProgressInfo>? progressCallback, List<string> tempFiles)
        {
            var fileInfo = new FileInfo(mediaFilePath);
            
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 5,
                StatusMessage = $"檔案過大 ({fileInfo.Length / 1024 / 1024:F1} MB)，開始精準分塊處理...",
                IsTranscriptionProgress = true,
                CurrentStep = "分塊處理",
                TotalSteps = 7,
                CurrentStepIndex = 2
            });

            // 階段一：標準化為 WAV
            var standardizedWavPath = await StandardizeToWavAsync(mediaFilePath, progressCallback);
            tempFiles.Add(standardizedWavPath);

            // 階段二：計算安全分割時長
            var safeDurationInSeconds = await CalculateSafeDurationAsync(standardizedWavPath, progressCallback);

            // 階段三：根據安全時長進行分割
            var chunkPaths = await SplitWavFileAsync(standardizedWavPath, safeDurationInSeconds, progressCallback);
            tempFiles.AddRange(chunkPaths);

            // 階段四：循環處理每個區塊並校正時間戳
            var finalSubtitles = new List<SubtitleItem>();
            var cumulativeOffset = TimeSpan.Zero;
            var totalChunks = chunkPaths.Count;

            for (int i = 0; i < totalChunks; i++)
            {
                var chunkPath = chunkPaths[i];
                var baseProgress = 40 + (50 * i / totalChunks); // 40% - 90%

                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = baseProgress,
                    StatusMessage = $"處理區塊 {i + 1}/{totalChunks}...",
                    IsTranscriptionProgress = true,
                    CurrentStep = $"轉錄區塊 {i + 1}",
                    TotalSteps = 7,
                    CurrentStepIndex = 5
                });

                // 呼叫 OpenAI API 處理當前區塊
                var response = await CallOpenAiApiAsync(chunkPath, language, null);
                var chunkSubtitles = ConvertResponseToSubtitles(response);

                // 校正時間戳並加入最終結果
                foreach (var subtitle in chunkSubtitles)
                {
                    var originalStartTime = subtitle.StartTime;
                    var originalEndTime = subtitle.EndTime;
                    
                    // 解析原始時間並加上累積偏移量
                    var startTime = ParseTimeStamp(subtitle.StartTime).Add(cumulativeOffset);
                    var endTime = ParseTimeStamp(subtitle.EndTime).Add(cumulativeOffset);
                    
                    subtitle.StartTime = FormatTimeSpan(startTime);
                    subtitle.EndTime = FormatTimeSpan(endTime);
                    subtitle.Index = finalSubtitles.Count + 1;
                    
                    System.Diagnostics.Debug.WriteLine($"區塊 {i + 1} 時間校正: [{originalStartTime}] -> [{subtitle.StartTime}], [{originalEndTime}] -> [{subtitle.EndTime}]");
                    
                    finalSubtitles.Add(subtitle);
                }

                // 獲取當前區塊的精確時長並累加到偏移量
                var chunkDuration = await GetAudioDurationAsync(chunkPath);
                cumulativeOffset = cumulativeOffset.Add(chunkDuration);
                
                System.Diagnostics.Debug.WriteLine($"區塊 {i + 1} 處理完成，時長: {chunkDuration.TotalSeconds:F1}s，累積偏移: {cumulativeOffset.TotalSeconds:F1}s");
            }

            // 最終整理
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 95,
                StatusMessage = "合併轉錄結果...",
                IsTranscriptionProgress = true,
                CurrentStep = "結果合併",
                TotalSteps = 7,
                CurrentStepIndex = 6
            });

            // 重新編號
            for (int i = 0; i < finalSubtitles.Count; i++)
            {
                finalSubtitles[i].Index = i + 1;
            }

            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 100,
                StatusMessage = $"分塊轉錄完成！共生成 {finalSubtitles.Count} 條字幕",
                IsTranscriptionProgress = true,
                CurrentStep = "完成",
                TotalSteps = 7,
                CurrentStepIndex = 7
            });

            return finalSubtitles;
        }

        /// <summary>
        /// 階段一：將任何格式的影音檔標準化為 WAV
        /// </summary>
        /// <param name="mediaFilePath">原始媒體檔案路徑</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>標準化後的 WAV 檔案路徑</returns>
        private async Task<string> StandardizeToWavAsync(string mediaFilePath, IProgress<AiProgressInfo>? progressCallback)
        {
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 10,
                StatusMessage = "正在標準化音訊格式為 WAV...",
                IsTranscriptionProgress = true,
                CurrentStep = "音訊標準化",
                TotalSteps = 7,
                CurrentStepIndex = 2
            });

            var tempDir = Path.Combine(Path.GetTempPath(), "SubtitleEditor_Temp", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            var wavPath = Path.Combine(tempDir, "temp_full_audio.wav");

            var mediaInfo = await FFmpeg.GetMediaInfo(mediaFilePath);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            
            if (audioStream == null)
            {
                throw new InvalidOperationException("找不到音訊軌道");
            }

            // 轉換為 Whisper 最佳格式：16kHz, 16-bit, 單聲道
            await FFmpeg.Conversions.New()
                .AddStream(audioStream.SetSampleRate(16000).SetBitrate(256).SetChannels(1))
                .SetOutput(wavPath)
                .Start();

            System.Diagnostics.Debug.WriteLine($"音訊標準化完成: {wavPath}");
            
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 20,
                StatusMessage = "音訊標準化完成",
                IsTranscriptionProgress = true,
                CurrentStep = "標準化完成",
                TotalSteps = 7,
                CurrentStepIndex = 3
            });

            return wavPath;
        }

        /// <summary>
        /// 階段二：計算安全分割時長
        /// </summary>
        /// <param name="wavPath">標準化的 WAV 檔案路徑</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>安全的分割時長（秒）</returns>
        private async Task<double> CalculateSafeDurationAsync(string wavPath, IProgress<AiProgressInfo>? progressCallback)
        {
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 25,
                StatusMessage = "計算最佳分割時長...",
                IsTranscriptionProgress = true,
                CurrentStep = "計算分割",
                TotalSteps = 7,
                CurrentStepIndex = 3
            });

            var fileInfo = new FileInfo(wavPath);
            var mediaInfo = await FFmpeg.GetMediaInfo(wavPath);
            var totalDurationInSeconds = mediaInfo.Duration.TotalSeconds;
            
            // 計算每秒位元組數
            var bytesPerSecond = fileInfo.Length / totalDurationInSeconds;
            
            // 計算安全時長（使用 24MB 作為安全限制）
            var safeDurationInSeconds = SafeFileSizeBytes / bytesPerSecond;
            
            // 確保每個區塊至少 30 秒，避免過度分割
            safeDurationInSeconds = Math.Max(safeDurationInSeconds, 30);
            
            var estimatedChunks = (int)Math.Ceiling(totalDurationInSeconds / safeDurationInSeconds);
            
            System.Diagnostics.Debug.WriteLine($"檔案分析: 總時長={totalDurationInSeconds:F1}s, 檔案大小={fileInfo.Length / 1024 / 1024:F1}MB");
            System.Diagnostics.Debug.WriteLine($"計算結果: 每秒{bytesPerSecond:F0}位元組, 安全時長={safeDurationInSeconds:F1}s, 預估{estimatedChunks}個區塊");
            
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 30,
                StatusMessage = $"計算完成：預估分割為 {estimatedChunks} 個區塊",
                IsTranscriptionProgress = true,
                CurrentStep = "分割計算完成",
                TotalSteps = 7,
                CurrentStepIndex = 3
            });

            return safeDurationInSeconds;
        }

        /// <summary>
        /// 階段三：根據安全時長分割 WAV 檔案
        /// </summary>
        /// <param name="wavPath">標準化的 WAV 檔案路徑</param>
        /// <param name="safeDurationInSeconds">安全分割時長</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>分割後的區塊檔案路徑列表</returns>
        private async Task<List<string>> SplitWavFileAsync(string wavPath, double safeDurationInSeconds, IProgress<AiProgressInfo>? progressCallback)
        {
            var chunkPaths = new List<string>();
            var tempDir = Path.GetDirectoryName(wavPath) ?? Path.GetTempPath();
            
            var mediaInfo = await FFmpeg.GetMediaInfo(wavPath);
            var totalDuration = mediaInfo.Duration.TotalSeconds;
            var audioStream = mediaInfo.AudioStreams.First();
            
            var chunkCount = (int)Math.Ceiling(totalDuration / safeDurationInSeconds);
            
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 35,
                StatusMessage = $"開始分割音訊檔案為 {chunkCount} 個區塊...",
                IsTranscriptionProgress = true,
                CurrentStep = "音訊分割",
                TotalSteps = 7,
                CurrentStepIndex = 4
            });

            for (int i = 0; i < chunkCount; i++)
            {
                var startTime = TimeSpan.FromSeconds(i * safeDurationInSeconds);
                var duration = TimeSpan.FromSeconds(Math.Min(safeDurationInSeconds, totalDuration - (i * safeDurationInSeconds)));
                
                var chunkPath = Path.Combine(tempDir, $"chunk_{i + 1:D3}.wav");
                
                await FFmpeg.Conversions.New()
                    .AddStream(audioStream)
                    .SetSeek(startTime)
                    .SetOutputTime(duration)
                    .SetOutput(chunkPath)
                    .Start();
                
                chunkPaths.Add(chunkPath);
                
                System.Diagnostics.Debug.WriteLine($"分割區塊 {i + 1}: 開始={startTime.TotalSeconds:F1}s, 時長={duration.TotalSeconds:F1}s, 檔案={Path.GetFileName(chunkPath)}");
                
                var progress = 35 + (5 * (i + 1) / chunkCount); // 35% - 40%
                progressCallback?.Report(new AiProgressInfo
                {
                    ProgressPercentage = progress,
                    StatusMessage = $"分割進度 {i + 1}/{chunkCount}",
                    IsTranscriptionProgress = true,
                    CurrentStep = "音訊分割",
                    TotalSteps = 7,
                    CurrentStepIndex = 4
                });
            }

            System.Diagnostics.Debug.WriteLine($"音訊分割完成，共 {chunkPaths.Count} 個區塊");
            return chunkPaths;
        }

        /// <summary>
        /// 獲取音訊檔案的精確時長
        /// </summary>
        /// <param name="audioPath">音訊檔案路徑</param>
        /// <returns>音訊時長</returns>
        private async Task<TimeSpan> GetAudioDurationAsync(string audioPath)
        {
            var mediaInfo = await FFmpeg.GetMediaInfo(audioPath);
            return mediaInfo.Duration;
        }

        /// <summary>
        /// 解析時間戳記字串為 TimeSpan
        /// </summary>
        /// <param name="timeStamp">時間戳記字串</param>
        /// <returns>TimeSpan 物件</returns>
        private TimeSpan ParseTimeStamp(string timeStamp)
        {
            try
            {
                // 支援多種格式：HH:MM:SS,mmm 或 HH:MM:SS.mmm
                var normalizedTimeStamp = timeStamp.Replace(',', '.');
                
                var formats = new[]
                {
                    @"HH\:mm\:ss\.fff",  // HH:MM:SS.mmm (3位毫秒)
                    @"HH\:mm\:ss\.ff",   // HH:MM:SS.mm  (2位毫秒)
                    @"HH\:mm\:ss\.f",    // HH:MM:SS.m   (1位毫秒)
                    @"HH\:mm\:ss"        // HH:MM:SS     (無毫秒)
                };

                foreach (var format in formats)
                {
                    if (TimeSpan.TryParseExact(normalizedTimeStamp, format, null, out var time))
                    {
                        return time;
                    }
                }
                
                // 如果都解析失敗，嘗試直接解析
                if (TimeSpan.TryParse(normalizedTimeStamp, out var fallbackTime))
                {
                    return fallbackTime;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"時間戳記解析錯誤: '{timeStamp}', 錯誤: {ex.Message}");
            }

            // 解析失敗時返回零時間
            System.Diagnostics.Debug.WriteLine($"無法解析時間戳記: '{timeStamp}', 返回零時間");
            return TimeSpan.Zero;
        }

        /// <summary>
        /// 驗證配置設定
        /// </summary>
        private void ValidateConfiguration()
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API Key 未設定。\n\n" +
                    "請在應用程式設定中加入您的 OpenAI API Key：\n" +
                    "1. 前往 https://platform.openai.com/api-keys 取得 API Key\n" +
                    "2. 在 appsettings.json 中加入：\"OpenAI:ApiKey\": \"your-api-key-here\"\n" +
                    "3. 或透過應用程式設定介面進行設定\n\n" +
                    "注意：請確保 API Key 有存取 Whisper API 的權限。");
            }
        }

        /// <summary>
        /// 取得API Key（優先使用暫時設定的，否則使用配置檔案中的）
        /// </summary>
        /// <returns>API Key</returns>
        private string GetApiKey()
        {
            // 優先使用暫時設定的API Key
            if (!string.IsNullOrWhiteSpace(_temporaryApiKey))
            {
                return _temporaryApiKey;
            }

            // 否則使用配置檔案中的API Key
            var configApiKey = _configuration["OpenAI:ApiKey"] ?? string.Empty;
            return configApiKey;
        }

        /// <summary>
        /// 驗證輸入檔案
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        private static void ValidateInputFile(string mediaFilePath)
        {
            if (string.IsNullOrWhiteSpace(mediaFilePath))
            {
                throw new ArgumentException("媒體檔案路徑不能為空", nameof(mediaFilePath));
            }

            if (!File.Exists(mediaFilePath))
            {
                throw new FileNotFoundException($"找不到媒體檔案：{mediaFilePath}");
            }

            // 檢查支援的檔案格式
            var supportedExtensions = new[] { ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm", ".avi", ".mov", ".flv", ".mkv" };
            var extension = Path.GetExtension(mediaFilePath).ToLowerInvariant();
            
            if (!supportedExtensions.Contains(extension))
            {
                throw new NotSupportedException($"不支援的檔案格式：{extension}\n\n支援的格式：{string.Join(", ", supportedExtensions)}");
            }
        }

        /// <summary>
        /// 呼叫 OpenAI API
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <param name="language">語言設定</param>
        /// <param name="progressCallback">進度回調函數</param>
        /// <returns>API 回應</returns>
        private async Task<OpenAiTranscriptionResponse> CallOpenAiApiAsync(string mediaFilePath, string language, IProgress<AiProgressInfo>? progressCallback)
        {
            var apiKey = GetApiKey();
            
            // 設定 HTTP 標頭
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // 準備 multipart/form-data
            using var content = new MultipartFormDataContent();
            
            // 添加檔案
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(mediaFilePath));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(mediaFilePath));

            // 添加模型參數
            content.Add(new StringContent("whisper-1"), "model");

            // 添加回應格式（verbose_json 包含時間戳記）
            content.Add(new StringContent("verbose_json"), "response_format");

            // 添加語言設定（如果不是自動偵測）
            var languageCode = MapLanguageToCode(language);
            if (!string.IsNullOrEmpty(languageCode) && languageCode != "auto")
            {
                content.Add(new StringContent(languageCode), "language");
            }

            // 發送請求
            progressCallback?.Report(new AiProgressInfo
            {
                ProgressPercentage = 50,
                StatusMessage = "正在進行雲端轉錄...",
                IsTranscriptionProgress = true,
                CurrentStep = "雲端處理",
                TotalSteps = 4,
                CurrentStepIndex = 3
            });

            var response = await _httpClient.PostAsync(OpenAiApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"OpenAI API 錯誤 ({response.StatusCode}): {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var transcriptionResponse = JsonSerializer.Deserialize<OpenAiTranscriptionResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return transcriptionResponse ?? throw new InvalidOperationException("API 回應格式無效");
        }

        /// <summary>
        /// 將語言名稱對應到 OpenAI API 語言代碼
        /// </summary>
        /// <param name="language">語言名稱</param>
        /// <returns>語言代碼</returns>
        private static string MapLanguageToCode(string language)
        {
            return language switch
            {
                "自動偵測" => "auto",
                "繁體中文" or "簡體中文" => "zh",
                "英文" => "en",
                "日文" => "ja",
                "韓文" => "ko",
                "法文" => "fr",
                "德文" => "de",
                "西班牙文" => "es",
                "俄文" => "ru",
                "阿拉伯文" => "ar",
                "葡萄牙文" => "pt",
                "義大利文" => "it",
                "荷蘭文" => "nl",
                "土耳其文" => "tr",
                "波蘭文" => "pl",
                "瑞典文" => "sv",
                "丹麥文" => "da",
                "挪威文" => "no",
                "芬蘭文" => "fi",
                _ => "auto"
            };
        }

        /// <summary>
        /// 將 OpenAI API 回應轉換為字幕項目
        /// </summary>
        /// <param name="response">API 回應</param>
        /// <returns>字幕項目列表</returns>
        private static List<SubtitleItem> ConvertResponseToSubtitles(OpenAiTranscriptionResponse response)
        {
            var subtitles = new List<SubtitleItem>();

            if (response.Segments != null && response.Segments.Any())
            {
                System.Diagnostics.Debug.WriteLine($"OpenAI API 回應包含 {response.Segments.Count} 個片段");
                
                // 使用詳細的時間戳記資訊
                for (int i = 0; i < response.Segments.Count; i++)
                {
                    var segment = response.Segments[i];
                    var startTime = FormatTimeSpan(TimeSpan.FromSeconds(segment.Start));
                    var endTime = FormatTimeSpan(TimeSpan.FromSeconds(segment.End));
                    
                    System.Diagnostics.Debug.WriteLine($"  片段 {i + 1}: {segment.Start:F1}s-{segment.End:F1}s -> [{startTime}]-[{endTime}] 文字: '{segment.Text.Trim()}'");
                    
                    subtitles.Add(new SubtitleItem
                    {
                        Index = i + 1,
                        StartTime = startTime,
                        EndTime = endTime,
                        Speaker = "Speaker1", // OpenAI API 不提供說話者識別
                        Text = segment.Text.Trim()
                    });
                }
            }
            else if (!string.IsNullOrEmpty(response.Text))
            {
                // 如果沒有詳細時間戳記，創建單一字幕項目
                subtitles.Add(new SubtitleItem
                {
                    Index = 1,
                    StartTime = "00:00:00,000",
                    EndTime = "00:00:10,000", // 預設 10 秒
                    Speaker = "Speaker1",
                    Text = response.Text.Trim()
                });
            }

            return subtitles;
        }

        /// <summary>
        /// 格式化時間跨度為字幕時間格式
        /// </summary>
        /// <param name="timeSpan">時間跨度</param>
        /// <returns>格式化的時間字串</returns>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00},{timeSpan.Milliseconds:000}";
        }

        /// <summary>
        /// 清理暫時檔案
        /// </summary>
        /// <param name="tempFiles">要清理的檔案路徑列表</param>
        private static async Task CleanupTempFilesAsync(List<string> tempFiles)
        {
            await Task.Run(() =>
            {
                foreach (var file in tempFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"已刪除暫存檔案: {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"刪除暫存檔案失敗: {file}, 錯誤: {ex.Message}");
                    }
                }

                // 嘗試清理暫時目錄
                var tempDirs = tempFiles
                    .Select(Path.GetDirectoryName)
                    .Where(dir => !string.IsNullOrEmpty(dir))
                    .Distinct()
                    .ToList();

                foreach (var dir in tempDirs)
                {
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            System.Diagnostics.Debug.WriteLine($"已刪除暫存目錄: {dir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"刪除暫存目錄失敗: {dir}, 錯誤: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// 釋放資源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #region IAiTranscriptionService 舊版本相容方法

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕
        /// </summary>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language) 方法")]
        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode)
        {
            var results = await TranscribeInternalAsync(videoPath, "whisper-1", language, null);
            return results.ToList();
        }

        /// <summary>
        /// 舊版本相容方法 - 非同步轉錄影片為字幕，支援進度報告
        /// </summary>
        [Obsolete("請使用新的 TranscribeAsync(string mediaFilePath, string modelName, string language, IProgress<AiProgressInfo>) 方法")]
        public async Task<List<SubtitleItem>> TranscribeAsync(string videoPath, string language, string generationMode, string segmentationMode, IProgress<AiProgressInfo>? progressCallback)
        {
            var results = await TranscribeInternalAsync(videoPath, "whisper-1", language, progressCallback);
            return results.ToList();
        }

        #endregion

        #region OpenAI API 回應模型

        /// <summary>
        /// OpenAI 轉錄 API 回應模型
        /// </summary>
        private class OpenAiTranscriptionResponse
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;

            [JsonPropertyName("language")]
            public string Language { get; set; } = string.Empty;

            [JsonPropertyName("duration")]
            public double Duration { get; set; }

            [JsonPropertyName("segments")]
            public List<OpenAiSegment>? Segments { get; set; }
        }

        /// <summary>
        /// OpenAI 轉錄片段模型
        /// </summary>
        private class OpenAiSegment
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("start")]
            public double Start { get; set; }

            [JsonPropertyName("end")]
            public double End { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        #endregion
    }
} 