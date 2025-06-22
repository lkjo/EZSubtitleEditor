using Prism.Commands;
using Prism.Mvvm;
using Prism.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using SubtitleEditor.Common.Enums;

namespace SubtitleEditor.UI.ViewModels
{
    public class AiSettingsViewModel : BindableBase, IDialogAware
    {
        private string _selectedLanguage;
        private string _selectedGenerationMode;
        private string _selectedSegmentationMode;
        private string _selectedServiceType;
        private string _apiKey;

        public AiSettingsViewModel()
        {
            InitializeServiceTypes();
            InitializeLanguages();
            InitializeGenerationModes();
            InitializeSegmentationModes();
            
            // 設定預設值
            SelectedServiceType = "本地服務 (Whisper.NET)";
            SelectedLanguage = "繁體中文";
            SelectedGenerationMode = "小型模型 (base)";
            SelectedSegmentationMode = "字幕分段";

            // 初始化命令
            AcceptCommand = new DelegateCommand(OnAccept, CanAccept);
            CancelCommand = new DelegateCommand(OnCancel);
            OpenApiKeyUrlCommand = new DelegateCommand(OnOpenApiKeyUrl);

            // 監聽屬性變更以更新命令狀態
            PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(SelectedServiceType) || e.PropertyName == nameof(ApiKey))
                {
                    AcceptCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(IsCloudServiceSelected));
                    RaisePropertyChanged(nameof(ModelDescription));
                    RaisePropertyChanged(nameof(ServiceDescription));
                }
                else if (e.PropertyName == nameof(SelectedGenerationMode))
                {
                    RaisePropertyChanged(nameof(ModelDescription));
                }
            };
        }

        #region 初始化方法

        private void InitializeServiceTypes()
        {
            ServiceTypes = new List<string> 
            { 
                "本地服務 (Whisper.NET)", 
                "雲端服務 (OpenAI)" 
            };
        }

        private void InitializeLanguages()
        {
            Languages = new List<string> 
            { 
                "自動偵測", 
                "繁體中文", 
                "簡體中文", 
                "英文", 
                "日文", 
                "韓文", 
                "法文", 
                "德文", 
                "西班牙文", 
                "俄文", 
                "阿拉伯文",
                "葡萄牙文",
                "義大利文",
                "荷蘭文",
                "土耳其文",
                "波蘭文",
                "瑞典文",
                "丹麥文",
                "挪威文",
                "芬蘭文"
            };
        }

        private void InitializeGenerationModes()
        {
            GenerationModes = new List<string> 
            { 
                "輕型模型 (tiny)", 
                "小型模型 (base)", 
                "中型模型 (medium)", 
                "大型模型 (large)" 
            };
        }

        private void InitializeSegmentationModes()
        {
            SegmentationModes = new List<string> 
            { 
                "字幕分段", 
                "依據演講者分段" 
            };
        }

        #endregion

        #region IDialogAware 實作

        public string Title => "AI 字幕設定";

        public DialogCloseListener RequestClose { get; }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            // 目前留空
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 檢查是否強制雲端服務模式
            if (parameters.ContainsKey("ForceCloudService") && 
                parameters.GetValue<bool>("ForceCloudService"))
            {
                SelectedServiceType = "雲端服務 (OpenAI)";
                // 可以考慮隱藏服務類型選擇器，但暫時保持可見
            }
            
            // 檢查是否強制本地服務模式
            if (parameters.ContainsKey("ForceLocalService") && 
                parameters.GetValue<bool>("ForceLocalService"))
            {
                SelectedServiceType = "本地服務 (Whisper.NET)";
                // 可以考慮隱藏服務類型選擇器，但暫時保持可見
            }
        }

        #endregion

        #region 屬性

        public List<string> ServiceTypes { get; private set; }

        public string SelectedServiceType
        {
            get => _selectedServiceType;
            set
            {
                if (SetProperty(ref _selectedServiceType, value))
                {
                    // 通知相關屬性變更
                    RaisePropertyChanged(nameof(IsCloudServiceSelected));
                    RaisePropertyChanged(nameof(IsLocalServiceSelected));
                    RaisePropertyChanged(nameof(ModelDescription));
                    RaisePropertyChanged(nameof(ServiceDescription));
                    // 只有在命令已初始化時才呼叫
                    AcceptCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCloudServiceSelected => SelectedServiceType == "雲端服務 (OpenAI)";
        
        public bool IsLocalServiceSelected => SelectedServiceType == "本地服務 (Whisper.NET)";

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public List<string> Languages { get; private set; }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        public List<string> GenerationModes { get; private set; }

        public string SelectedGenerationMode
        {
            get => _selectedGenerationMode;
            set => SetProperty(ref _selectedGenerationMode, value);
        }

        public string ModelDescription
        {
            get
            {
                if (IsCloudServiceSelected)
                {
                    return "雲端服務使用 OpenAI 的 Whisper-1 模型，具有優秀的轉錄準確度和多語言支援。";
                }

                return SelectedGenerationMode switch
                {
                    "輕型模型 (tiny)" => "最快速度，適合快速測試，準確度較低 (~39 MB RAM)",
                    "小型模型 (base)" => "平衡速度與準確度，適合一般使用 (~74 MB RAM)",
                    "中型模型 (medium)" => "較高準確度，處理時間較長 (~769 MB RAM)",
                    "大型模型 (large)" => "最高準確度，需要較長處理時間 (~1550 MB RAM)",
                    _ => "選擇適合您需求的模型大小"
                };
            }
        }

        public string ServiceDescription
        {
            get
            {
                return IsCloudServiceSelected
                    ? "雲端服務特點：\n• 無需下載模型，節省磁碟空間\n• 使用最新的 OpenAI Whisper 技術\n• 需要穩定的網路連線\n• 檔案大小限制 25MB\n• 需要 OpenAI API Key（付費服務）"
                    : "本地服務特點：\n• 完全離線運作，保護隱私\n• 首次使用自動下載模型\n• 無檔案大小限制\n• 一次下載，永久使用\n• 處理速度取決於硬體效能";
            }
        }

        public List<string> SegmentationModes { get; private set; }

        public string SelectedSegmentationMode
        {
            get => _selectedSegmentationMode;
            set => SetProperty(ref _selectedSegmentationMode, value);
        }

        #endregion

        #region 命令

        public DelegateCommand AcceptCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand OpenApiKeyUrlCommand { get; }

        private bool CanAccept()
        {
            // 如果選擇雲端服務，必須提供 API Key
            if (IsCloudServiceSelected)
            {
                return !string.IsNullOrWhiteSpace(ApiKey);
            }
            
            return true; // 本地服務不需要額外驗證
        }

        private void OnAccept()
        {
            try
            {
                // 確定服務類型
                var serviceType = IsCloudServiceSelected ? AiServiceType.Cloud : AiServiceType.Local;

                var parameters = new DialogParameters
                {
                    { "ServiceType", serviceType },
                    { "Language", SelectedLanguage },
                    { "GenerationMode", SelectedGenerationMode },
                    { "SegmentationMode", SelectedSegmentationMode }
                };

                // 如果是雲端服務，包含 API Key
                if (IsCloudServiceSelected)
                {
                    // System.Diagnostics.Debug.WriteLine($"[DEBUG] AiSettingsViewModel 傳遞 API Key: {ApiKey?.Substring(0, Math.Min(10, ApiKey?.Length ?? 0))}...");
                    parameters.Add("ApiKey", ApiKey);
                }

                RequestClose.Invoke(parameters, ButtonResult.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定儲存失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancel()
        {
            RequestClose.Invoke(ButtonResult.Cancel);
        }

        private void OnOpenApiKeyUrl()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://platform.openai.com/api-keys",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟網頁：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }
} 