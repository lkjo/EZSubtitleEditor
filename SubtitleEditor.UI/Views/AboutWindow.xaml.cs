using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace SubtitleEditor.UI.Views
{
    /// <summary>
    /// AboutWindow.xaml 的互動邏輯
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 處理超連結點擊事件
        /// </summary>
        /// <param name="sender">事件發送者</param>
        /// <param name="e">事件參數</param>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // 使用預設瀏覽器開啟URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"無法開啟瀏覽器：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 處理確定按鈕點擊事件
        /// </summary>
        /// <param name="sender">事件發送者</param>
        /// <param name="e">事件參數</param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
} 