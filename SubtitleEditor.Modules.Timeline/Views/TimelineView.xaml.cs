using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SubtitleEditor.Modules.Timeline.ViewModels;

namespace SubtitleEditor.Modules.Timeline.Views
{
    /// <summary>
    /// TimelineView.xaml 的互動邏輯
    /// 使用 CompositionTarget.Rendering + WPF動畫實現極致流暢的時間軸捲動效果
    /// </summary>
    public partial class TimelineView : UserControl
    {
        private bool _isAnimating = false; // 避免動畫衝突
        private double _currentDisplayX = 0; // 當前顯示的X位置
        private const double MovementThreshold = 1.0; // 位置變化閾值（像素）

        public TimelineView()
        {
            InitializeComponent();
            
            // 掛載渲染事件，實現精確的時間追蹤
            CompositionTarget.Rendering += OnRendering;
            
            // 取消掛載，避免記憶體洩漏
            this.Unloaded += (s, e) => CompositionTarget.Rendering -= OnRendering;
            
            // 當DataContext變化時，設定初始位置
            this.DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// 當DataContext變化時設定初始位置
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TimelineViewModel viewModel)
            {
                // 延遲設定初始位置，確保ViewModel完全初始化
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetInitialPosition(viewModel);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 設定初始位置：讓時間0點顯示在畫面中央
        /// </summary>
        private void SetInitialPosition(TimelineViewModel viewModel)
        {
            try
            {
                // 確保有可視寬度資訊
                if (this.ActualWidth > 0)
                {
                    viewModel.SetViewportWidth(this.ActualWidth);
                }

                // 計算初始位置：讓時間0點（位於LeadingSpaceWidth位置）顯示在視窗中央
                // 因為時間0點在Canvas內容中的位置是LeadingSpaceWidth，
                // 所以要讓它顯示在視窗中央，Canvas需要向左移動LeadingSpaceWidth距離
                var initialX = 0.0 - viewModel.LeadingSpaceWidth; // 初始狀態：時間0點正好在視窗中央
                
                // 直接設定位置，不使用動畫
                var canvasTransform = TimelineCanvas.RenderTransform as TranslateTransform;
                var markerTransform = TimeMarkerCanvas.RenderTransform as TranslateTransform;
                System.Diagnostics.Debug.WriteLine($"可能的錯誤:{canvasTransform.X}, {markerTransform.X}");
                
                if (canvasTransform != null)
                    canvasTransform.X = initialX;
                    
                if (markerTransform != null)
                    markerTransform.X = initialX;
                    
                _currentDisplayX = initialX;
                
                System.Diagnostics.Debug.WriteLine($"Timeline 設定初始位置: {initialX:F1}, 可視寬度: {viewModel.ViewportWidth:F1}, LeadingSpaceWidth: {viewModel.LeadingSpaceWidth:F1}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定初始位置失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染事件處理方法 - 精確控制動畫觸發時機
        /// </summary>
        private void OnRendering(object? sender, EventArgs e)
        {
            // 避免動畫衝突
            if (_isAnimating)
                return;

            // 1. 取得 ViewModel
            var viewModel = this.DataContext as TimelineViewModel;
            if (viewModel == null)
                return;

            // 2. 獲取當前播放時間
            var currentTime = viewModel.CurrentTime;
            
            // 3. 計算目標X位置：讓當前時間點顯示在畫面中央
            // 公式：LeadingSpaceWidth - (當前時間的像素位置)
            var currentTimePixels = currentTime.TotalSeconds * TimelineViewModel.PixelsPerSecond;
            var targetX = viewModel.LeadingSpaceWidth - currentTimePixels;
            
            // 4. 只在位置變化超過閾值時才啟動動畫
            if (Math.Abs(targetX - _currentDisplayX) > MovementThreshold)
            {
                AnimateToPosition(targetX, viewModel);
            }
        }

        /// <summary>
        /// 執行平滑動畫到指定位置
        /// </summary>
        /// <param name="targetX">目標 X 座標</param>
        /// <param name="viewModel">ViewModel實例</param>
        private void AnimateToPosition(double targetX, TimelineViewModel viewModel)
        {
            try
            {
                _isAnimating = true;
                _currentDisplayX = targetX; // 更新當前顯示位置

                // 取得Transform物件
                var canvasTransform = TimelineCanvas.RenderTransform as TranslateTransform;
                var markerTransform = TimeMarkerCanvas.RenderTransform as TranslateTransform;

                if (canvasTransform == null || markerTransform == null)
                {
                    _isAnimating = false;
                    return;
                }

                // 建立主時間軸的動畫（較短的動畫時間保持響應性）
                var timelineAnimation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = new Duration(TimeSpan.FromMilliseconds(1000)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // 建立時間刻度的同步動畫
                var markerAnimation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = new Duration(TimeSpan.FromMilliseconds(1000)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // 動畫完成後重置標記
                timelineAnimation.Completed += (s, e) => _isAnimating = false;

                // 啟動動畫
                canvasTransform.BeginAnimation(TranslateTransform.XProperty, timelineAnimation);
                markerTransform.BeginAnimation(TranslateTransform.XProperty, markerAnimation);

                // Debug輸出（可選）
                // System.Diagnostics.Debug.WriteLine($"Timeline 動畫到位置: {targetX:F1}, 當前時間: {viewModel?.CurrentTime:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"執行動畫失敗: {ex.Message}");
                _isAnimating = false;
            }
        }

        /// <summary>
        /// 處理時間軸容器尺寸變化事件
        /// </summary>
        /// <param name="sender">事件發送者</param>
        /// <param name="e">尺寸變化事件參數</param>
        private void TimelineContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // 取得 ViewModel 實例並傳遞新的寬度
                if (DataContext is TimelineViewModel viewModel)
                {
                    viewModel.SetViewportWidth(e.NewSize.Width);
                    System.Diagnostics.Debug.WriteLine($"Timeline 容器尺寸變化: {e.NewSize.Width:F1} x {e.NewSize.Height:F1}");
                    
                    // 容器尺寸變化後，重新設定初始位置
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SetInitialPosition(viewModel);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"處理容器尺寸變化失敗: {ex.Message}");
            }
        }
    }
} 