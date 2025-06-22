using System.Collections.Generic;
using System.Threading.Tasks;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// 音訊處理服務介面
    /// </summary>
    public interface IAudioProcessingService
    {
        /// <summary>
        /// 生成音訊波形資料
        /// </summary>
        /// <param name="mediaFilePath">媒體檔案路徑</param>
        /// <returns>代表波形峰值的資料點列表</returns>
        Task<List<double>> GenerateWaveformAsync(string mediaFilePath);
    }
} 