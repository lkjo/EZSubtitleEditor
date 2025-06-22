using System.ComponentModel;
using System.Windows;
using SubtitleEditor.UI.ViewModels;

namespace SubtitleEditor.UI.Views
{
    /// <summary>
    /// Interaction logic for ShellView.xaml
    /// </summary>
    public partial class ShellView : Window
    {
        public ShellView()
        {
            InitializeComponent();
        }

        private void ShellView_Closing(object sender, CancelEventArgs e)
        {
            var viewModel = DataContext as ShellViewModel;
            
            // 如果正在進行 AI 轉換，顯示特別的確認對話框
            if (viewModel?.IsAiProcessing == true)
            {
                var aiResult = MessageBox.Show(
                    "正在進行 AI 字幕轉換中，確定要關閉嗎？\n\n關閉程式將中斷 AI 轉換過程。",
                    "AI 轉換進行中",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (aiResult == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            else
            {
                // 一般的保存提醒對話框
                var result = MessageBox.Show(
                    "您確定要結束嗎？\n\n請確保您已經保存了所有的字幕修改。",
                    "結束確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
} 