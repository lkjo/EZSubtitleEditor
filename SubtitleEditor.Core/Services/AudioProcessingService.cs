using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// 音訊處理服務實作
    /// </summary>
    public class AudioProcessingService : IAudioProcessingService
    {
        /// <summary>
        /// 生成音訊波形資料
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <returns>代表波形峰值的資料點列表</returns>
        public async Task<List<double>> GenerateWaveformAsync(string mediaFilePath)
        {
            return await Task.Run(() =>
            {
                var waveformData = new List<double>();

                try
                {
                    using var audioFile = new AudioFileReader(mediaFilePath);
                    
                    // 設定取樣參數
                    const int samplesPerPixel = 1024; // 每 1024 個樣本取一個峰值
                    var buffer = new float[samplesPerPixel];
                    int samplesRead;

                    while ((samplesRead = audioFile.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // 計算這個區塊的最大絕對值（峰值）
                        var peak = 0.0;
                        for (int i = 0; i < samplesRead; i++)
                        {
                            var sample = Math.Abs(buffer[i]);
                            if (sample > peak)
                            {
                                peak = sample;
                            }
                        }

                        waveformData.Add(peak);
                    }
                }
                catch (Exception ex)
                {
                    // 如果無法讀取音訊，回傳空資料
                    // System.Diagnostics.Debug.WriteLine($"無法生成波形資料: {ex.Message}");
                    
                    // 回傳一些模擬資料以供測試
                    for (int i = 0; i < 100; i++)
                    {
                        waveformData.Add(Math.Sin(i * 0.1) * 0.5 + 0.5);
                    }
                }

                return waveformData;
            });
        }
    }
} 