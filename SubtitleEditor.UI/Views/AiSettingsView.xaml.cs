using System.Windows.Controls;
using SubtitleEditor.UI.ViewModels;

namespace SubtitleEditor.UI.Views
{
    /// <summary>
    /// AiSettingsView.xaml 的互動邏輯
    /// </summary>
    public partial class AiSettingsView : UserControl
    {
        public AiSettingsView()
        {
            InitializeComponent();
            
            // 設定 PasswordBox 與 ViewModel 的連接
            this.Loaded += AiSettingsView_Loaded;
        }

        private void AiSettingsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is AiSettingsViewModel viewModel)
            {
                // 監聽 PasswordBox 密碼變更
                ApiKeyPasswordBox.PasswordChanged += (s, args) =>
                {
                    viewModel.ApiKey = ApiKeyPasswordBox.Password;
                };

                // 當 ViewModel 的 ApiKey 變更時更新 PasswordBox
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(viewModel.ApiKey) && 
                        ApiKeyPasswordBox.Password != viewModel.ApiKey)
                    {
                        ApiKeyPasswordBox.Password = viewModel.ApiKey ?? string.Empty;
                    }
                };
            }
        }
    }
} 