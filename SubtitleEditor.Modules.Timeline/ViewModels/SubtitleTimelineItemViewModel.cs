using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;
using SubtitleEditor.Common.Commands;

namespace SubtitleEditor.Modules.Timeline.ViewModels
{
    /// <summary>
    /// 時間軸上字幕項目的 ViewModel
    /// </summary>
    public class SubtitleTimelineItemViewModel : BindableBase
    {
        private readonly IUndoRedoService _undoRedoService;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private string _originalStartTime = string.Empty;
        private string _originalEndTime = string.Empty;
        private double _originalLeft = 0.0; // 記錄拖動開始時的原始位置
        private double _originalWidth = 0.0; // 記錄拖動開始時的原始寬度
        private double _pixelsPerSecond = 10.0; // 每秒對應的像素數，這個值需要從 TimelineViewModel 獲得

        /// <summary>
        /// 拖動模式列舉
        /// </summary>
        private enum DragMode
        {
            None,           // 無拖動
            Move,           // 移動整個區塊
            ResizeStart,    // 拉伸開始時間（左邊緣）
            ResizeEnd       // 拉伸結束時間（右邊緣）
        }

        private DragMode _currentDragMode = DragMode.None;

        /// <summary>
        /// 原始的字幕資料模型
        /// </summary>
        public SubtitleItem Model { get; }

        private double _left;
        /// <summary>
        /// 字幕塊在畫布上的左邊距
        /// </summary>
        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        private double _width;
        /// <summary>
        /// 字幕塊的寬度
        /// </summary>
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private string _displayText = string.Empty;
        /// <summary>
        /// 字幕塊中顯示的文字
        /// </summary>
        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        private bool _isActive;
        /// <summary>
        /// 用於高亮顯示
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// 滑鼠按下命令
        /// </summary>
        public DelegateCommand<MouseEventArgs> MouseDownCommand { get; }

        /// <summary>
        /// 滑鼠移動命令
        /// </summary>
        public DelegateCommand<MouseEventArgs> MouseMoveCommand { get; }

        /// <summary>
        /// 滑鼠放開命令
        /// </summary>
        public DelegateCommand<MouseEventArgs> MouseUpCommand { get; }

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="model">字幕資料模型</param>
        /// <param name="undoRedoService">Undo/Redo 服務</param>
        public SubtitleTimelineItemViewModel(SubtitleItem model, IUndoRedoService undoRedoService)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));

            // 初始化命令
            MouseDownCommand = new DelegateCommand<MouseEventArgs>(ExecuteMouseDown);
            MouseMoveCommand = new DelegateCommand<MouseEventArgs>(ExecuteMouseMove);
            MouseUpCommand = new DelegateCommand<MouseEventArgs>(ExecuteMouseUp);
        }

        /// <summary>
        /// 設定時間軸的像素比例
        /// </summary>
        /// <param name="pixelsPerSecond">每秒對應的像素數</param>
        public void SetPixelsPerSecond(double pixelsPerSecond)
        {
            _pixelsPerSecond = pixelsPerSecond;
            System.Diagnostics.Debug.WriteLine($"字幕項目 '{DisplayText}' 設定像素比例: {pixelsPerSecond} px/s");
        }

        /// <summary>
        /// 處理滑鼠按下事件
        /// </summary>
        /// <param name="e">滑鼠事件參數</param>
        private void ExecuteMouseDown(MouseEventArgs e)
        {
            if (e is MouseButtonEventArgs buttonArgs && buttonArgs.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _dragStartPoint = buttonArgs.GetPosition(null);
                
                // 記錄拖動開始時的原始時間和位置
                _originalStartTime = Model.StartTime;
                _originalEndTime = Model.EndTime;
                _originalLeft = Left;
                _originalWidth = Width;

                // 判斷拖動模式：檢查滑鼠點擊的相對位置
                _currentDragMode = DetermineDragMode(buttonArgs);

                // 捕獲滑鼠
                if (buttonArgs.Source is FrameworkElement element)
                {
                    element.CaptureMouse();
                }

                System.Diagnostics.Debug.WriteLine($"開始拖動字幕: {DisplayText}, 模式: {_currentDragMode}, 原始時間: {_originalStartTime} - {_originalEndTime}, 原始位置: {_originalLeft:F1}, 原始寬度: {_originalWidth:F1}");
            }
        }

        /// <summary>
        /// 判斷拖動模式
        /// </summary>
        /// <param name="e">滑鼠事件參數</param>
        /// <returns>拖動模式</returns>
        private DragMode DetermineDragMode(MouseButtonEventArgs e)
        {
            if (e.Source is FrameworkElement element)
            {
                // 檢查父級結構來判斷點擊的是哪個區域
                if (element.Parent is Grid grid)
                {
                    // 獲取element在Grid中的列索引
                    var columnIndex = Grid.GetColumn(element);
                    
                    System.Diagnostics.Debug.WriteLine($"點擊的列索引: {columnIndex}");
                    
                    switch (columnIndex)
                    {
                        case 0: // 左邊緣（拉伸開始時間）
                            return DragMode.ResizeStart;
                        case 1: // 中間區域（移動整個區塊）
                            return DragMode.Move;
                        case 2: // 右邊緣（拉伸結束時間）
                            return DragMode.ResizeEnd;
                        default:
                            return DragMode.Move;
                    }
                }
                
                // 如果無法通過Grid結構判斷，使用原始的位置判斷方法作為後備
                var position = e.GetPosition(element);
                var elementWidth = element.ActualWidth;

                // 定義拉伸把手的寬度（左右各5像素）
                const double handleWidth = 5.0;

                // 檢查是否點擊在左邊緣（拉伸開始時間）
                if (position.X <= handleWidth)
                {
                    return DragMode.ResizeStart;
                }
                // 檢查是否點擊在右邊緣（拉伸結束時間）
                else if (position.X >= elementWidth - handleWidth)
                {
                    return DragMode.ResizeEnd;
                }
                // 否則是中間區域（移動整個區塊）
                else
                {
                    return DragMode.Move;
                }
            }

            return DragMode.Move; // 預設為移動模式
        }

        /// <summary>
        /// 處理滑鼠移動事件
        /// </summary>
        /// <param name="e">滑鼠事件參數</param>
        private void ExecuteMouseMove(MouseEventArgs e)
        {
            if (_isDragging && e is MouseEventArgs mouseArgs)
            {
                var currentPosition = mouseArgs.GetPosition(null);
                var totalDeltaX = currentPosition.X - _dragStartPoint.X;

                switch (_currentDragMode)
                {
                    case DragMode.Move:
                        HandleMoveMode(totalDeltaX);
                        break;

                    case DragMode.ResizeStart:
                        HandleResizeStartMode(totalDeltaX);
                        break;

                    case DragMode.ResizeEnd:
                        HandleResizeEndMode(totalDeltaX);
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"拖動中: 模式={_currentDragMode}, TotalDelta={totalDeltaX:F1}px, Left={Left:F1}, Width={Width:F1}");
            }
        }

        /// <summary>
        /// 處理移動模式
        /// </summary>
        /// <param name="totalDeltaX">總的水平偏移量</param>
        private void HandleMoveMode(double totalDeltaX)
        {
            // 計算新的 Left 位置（基於原始位置加上總偏移量）
            var newLeft = _originalLeft + totalDeltaX;
            
            // 限制最小值為 0
            if (newLeft < 0)
                newLeft = 0;

            // 更新位置
            Left = newLeft;

            // 計算總的時間偏移並更新 Model 的時間（基於原始時間加上總偏移量）
            var totalTimeOffset = totalDeltaX / _pixelsPerSecond;
            UpdateModelTimeForMove(totalTimeOffset);
        }

        /// <summary>
        /// 處理拉伸開始時間模式（左邊緣）
        /// </summary>
        /// <param name="totalDeltaX">總的水平偏移量</param>
        private void HandleResizeStartMode(double totalDeltaX)
        {
            // 計算新的 Left 位置
            var newLeft = _originalLeft + totalDeltaX;
            
            // 計算新的寬度（左邊移動時，寬度相應變化）
            var newWidth = _originalWidth - totalDeltaX;

            // 限制最小值
            if (newLeft < 0)
            {
                // 如果左邊超出邊界，調整寬度補償
                newWidth += newLeft;
                newLeft = 0;
            }

            // 確保最小寬度（至少1秒）
            var minWidth = _pixelsPerSecond * 1.0; // 1秒的最小寬度
            if (newWidth < minWidth)
            {
                newLeft = _originalLeft + _originalWidth - minWidth;
                newWidth = minWidth;
            }

            // 更新位置和寬度
            Left = newLeft;
            Width = newWidth;

            // 更新模型時間
            UpdateModelTimeForResizeStart(totalDeltaX);
        }

        /// <summary>
        /// 處理拉伸結束時間模式（右邊緣）
        /// </summary>
        /// <param name="totalDeltaX">總的水平偏移量</param>
        private void HandleResizeEndMode(double totalDeltaX)
        {
            // 計算新的寬度（右邊移動時，寬度變化）
            var newWidth = _originalWidth + totalDeltaX;

            // 確保最小寬度（至少1秒）
            var minWidth = _pixelsPerSecond * 1.0; // 1秒的最小寬度
            if (newWidth < minWidth)
                newWidth = minWidth;

            // 更新寬度（Left 保持不變）
            Width = newWidth;

            // 更新模型時間
            UpdateModelTimeForResizeEnd(totalDeltaX);
        }

        /// <summary>
        /// 處理滑鼠放開事件
        /// </summary>
        /// <param name="e">滑鼠事件參數</param>
        private void ExecuteMouseUp(MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;

                // 釋放滑鼠捕獲
                if (e.Source is FrameworkElement element)
                {
                    element.ReleaseMouseCapture();
                }

                // 檢查時間是否真的有變化
                if (_originalStartTime != Model.StartTime || _originalEndTime != Model.EndTime)
                {
                    // 根據拖動模式建立對應的命令
                    switch (_currentDragMode)
                    {
                        case DragMode.Move:
                            var moveCommand = new MoveSubtitleCommand(Model, _originalStartTime, _originalEndTime);
                            _undoRedoService.Do(moveCommand);
                            break;

                        case DragMode.ResizeStart:
                        case DragMode.ResizeEnd:
                            // 解析時間
                            if (TryParseTime(_originalStartTime, out TimeSpan oldStart) &&
                                TryParseTime(_originalEndTime, out TimeSpan oldEnd) &&
                                TryParseTime(Model.StartTime, out TimeSpan newStart) &&
                                TryParseTime(Model.EndTime, out TimeSpan newEnd))
                            {
                                var resizeCommand = new ResizeSubtitleCommand(Model, oldStart, oldEnd, newStart, newEnd);
                                _undoRedoService.Do(resizeCommand);
                            }
                            break;
                    }

                    System.Diagnostics.Debug.WriteLine($"拖動完成: {DisplayText}, 模式: {_currentDragMode}, 新時間: {Model.StartTime} - {Model.EndTime}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"拖動完成: {DisplayText}, 時間未變化");
                }

                // 重設拖動模式
                _currentDragMode = DragMode.None;
            }
        }

        /// <summary>
        /// 根據時間偏移量更新 Model 的時間（移動模式）
        /// </summary>
        /// <param name="timeOffsetSeconds">時間偏移量（秒）</param>
        private void UpdateModelTimeForMove(double timeOffsetSeconds)
        {
            try
            {
                // 解析原始開始時間，支援多種時間格式
                TimeSpan originalStart;
                bool startParsed = TryParseTime(_originalStartTime, out originalStart);

                if (startParsed)
                {
                    var newStartTime = originalStart.Add(TimeSpan.FromSeconds(timeOffsetSeconds));
                    
                    // 確保時間不會是負數
                    if (newStartTime < TimeSpan.Zero)
                        newStartTime = TimeSpan.Zero;

                    // 解析原始結束時間，支援多種時間格式
                    TimeSpan originalEnd;
                    bool endParsed = TryParseTime(_originalEndTime, out originalEnd);

                    if (endParsed)
                    {
                        var newEndTime = originalEnd.Add(TimeSpan.FromSeconds(timeOffsetSeconds));
                        
                        // 確保結束時間不會是負數且不小於開始時間
                        if (newEndTime < TimeSpan.Zero)
                            newEndTime = TimeSpan.Zero;
                        if (newEndTime < newStartTime)
                            newEndTime = newStartTime.Add(TimeSpan.FromSeconds(1)); // 至少保持1秒的最小長度

                        // 格式化並更新 Model
                        Model.StartTime = FormatTimeSpan(newStartTime);
                        Model.EndTime = FormatTimeSpan(newEndTime);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新移動時間時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新模型時間（拉伸開始時間模式）
        /// </summary>
        /// <param name="deltaX">水平偏移量</param>
        private void UpdateModelTimeForResizeStart(double deltaX)
        {
            try
            {
                if (TryParseTime(_originalStartTime, out TimeSpan originalStart) &&
                    TryParseTime(_originalEndTime, out TimeSpan originalEnd))
                {
                    var timeOffset = deltaX / _pixelsPerSecond;
                    var newStartTime = originalStart.Add(TimeSpan.FromSeconds(timeOffset));
                    
                    // 確保開始時間不會是負數
                    if (newStartTime < TimeSpan.Zero)
                        newStartTime = TimeSpan.Zero;

                    // 確保開始時間不會超過結束時間（至少保持1秒的最小長度）
                    var minEndTime = newStartTime.Add(TimeSpan.FromSeconds(1));
                    var endTime = originalEnd;
                    if (endTime < minEndTime)
                        endTime = minEndTime;

                    // 更新模型
                    Model.StartTime = FormatTimeSpan(newStartTime);
                    Model.EndTime = FormatTimeSpan(endTime);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新拉伸開始時間時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新模型時間（拉伸結束時間模式）
        /// </summary>
        /// <param name="deltaX">水平偏移量</param>
        private void UpdateModelTimeForResizeEnd(double deltaX)
        {
            try
            {
                if (TryParseTime(_originalStartTime, out TimeSpan originalStart) &&
                    TryParseTime(_originalEndTime, out TimeSpan originalEnd))
                {
                    var timeOffset = deltaX / _pixelsPerSecond;
                    var newEndTime = originalEnd.Add(TimeSpan.FromSeconds(timeOffset));
                    
                    // 確保結束時間不會小於開始時間（至少保持1秒的最小長度）
                    var minEndTime = originalStart.Add(TimeSpan.FromSeconds(1));
                    if (newEndTime < minEndTime)
                        newEndTime = minEndTime;

                    // 更新模型
                    Model.StartTime = FormatTimeSpan(originalStart);
                    Model.EndTime = FormatTimeSpan(newEndTime);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新拉伸結束時間時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 嘗試解析時間字串
        /// </summary>
        /// <param name="timeString">時間字串</param>
        /// <param name="result">解析結果</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseTime(string timeString, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            return TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,f", null, out result) ||
                   TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,ff", null, out result) ||
                   TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,fff", null, out result);
        }

        /// <summary>
        /// 將 TimeSpan 格式化為字幕時間格式
        /// </summary>
        /// <param name="timeSpan">時間跨度</param>
        /// <returns>格式化的時間字串</returns>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // 使用十分之一秒精度，與SRT格式一致（如 00:00:00,0）
            var tenthsOfSecond = (timeSpan.Milliseconds + 50) / 100; // 四捨五入到十分之一秒
            if (tenthsOfSecond >= 10)
            {
                tenthsOfSecond = 9; // 最大為 9（因為是十分之一秒）
            }
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{tenthsOfSecond}";
        }
    }
} 