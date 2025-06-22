using System;
using Microsoft.Extensions.Configuration;
using Prism.Ioc;
using SubtitleEditor.Common.Enums;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// AI 服務工廠實作
    /// 根據服務類型提供對應的 AI 轉錄服務實例
    /// </summary>
    public class AiServiceFactory : IAiServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IContainerProvider _containerProvider;

        public AiServiceFactory(IConfiguration configuration, IContainerProvider containerProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _containerProvider = containerProvider ?? throw new ArgumentNullException(nameof(containerProvider));
        }

        /// <summary>
        /// 根據服務類型取得對應的 AI 轉錄服務
        /// </summary>
        /// <param name="serviceType">服務類型</param>
        /// <returns>AI 轉錄服務實例</returns>
        public IAiTranscriptionService GetService(AiServiceType serviceType)
        {
            return serviceType switch
            {
                AiServiceType.Local => CreateLocalService(),
                AiServiceType.Cloud => CreateCloudService(),
                _ => throw new ArgumentException($"不支援的服務類型：{serviceType}", nameof(serviceType))
            };
        }

        /// <summary>
        /// 建立本地 AI 服務實例
        /// </summary>
        /// <returns>WhisperNetService 實例</returns>
        private IAiTranscriptionService CreateLocalService()
        {
            try
            {
                // 使用容器來解析服務，如果已註冊的話
                if (_containerProvider.IsRegistered<WhisperNetService>())
                {
                    return _containerProvider.Resolve<WhisperNetService>();
                }
                
                // 否則直接建立新實例
                return new WhisperNetService();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"無法建立本地 AI 服務：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 建立雲端 AI 服務實例
        /// </summary>
        /// <returns>WhisperApiService 實例</returns>
        private IAiTranscriptionService CreateCloudService()
        {
            try
            {
                // 直接建立 WhisperApiService 實例
                // API Key 將通過 SetTemporaryApiKey 方法動態設定
                return new WhisperApiService(_configuration);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"無法建立雲端 AI 服務：{ex.Message}", ex);
            }
        }
    }
} 