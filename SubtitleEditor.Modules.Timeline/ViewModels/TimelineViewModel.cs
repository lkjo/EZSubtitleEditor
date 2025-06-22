using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Prism.Events;
using Prism.Mvvm;
using Prism.Commands;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Modules.Timeline.ViewModels
{
    /// <summary>
    /// Timeline 視圖模型
    /// </summary>
    public class TimelineViewModel : BindableBase, IDisposable
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IAudioProcessingService _audioService;
        private readonly IUndoRedoService _undoRedoService;
        
        /// <summary>
        /// 每秒對應的像素數（固定比例）
        /// </summary>
        public const double PixelsPerSecond = 100.0;
        
        /// <summary>
        /// ScrollViewer 可視區域的估計寬度（用於計算捲動偏移）
        /// </summary>
        private const double EstimatedViewportWidth = 800.0;

        /// <summary>
        /// 時間刻度間隔（秒）
        /// </summary>
        private const int TimeMarkerIntervalSeconds = 1;

        private PointCollection _waveformPoints = new();
        public PointCollection WaveformPoints
        {
            get => _waveformPoints;
            set => SetProperty(ref _waveformPoints, value);
        }

        private double _totalTimelineWidth = 1000.0;
        /// <summary>
        /// 時間軸畫布的總寬度
        /// </summary>
        public double TotalTimelineWidth
        {
            get => _totalTimelineWidth;
            set => SetProperty(ref _totalTimelineWidth, value);
        }

        /// <summary>
        /// ScrollViewer 可視區域的實際寬度
        /// </summary>
        public double ViewportWidth { get; set; } = 800.0;

        /// <summary>
        /// 前置空白區域的寬度（讓時間 0 能顯示在畫面中央）
        /// </summary>
        public double LeadingSpaceWidth => ViewportWidth / 2.0;

        /// <summary>
        /// 用來存放要在時間軸上顯示的字幕項
        /// </summary>
        public ObservableCollection<SubtitleTimelineItemViewModel> SubtitleItems { get; } = new();

        /// <summary>
        /// 時間刻度集合
        /// </summary>
        public ObservableCollection<TimeMarkerViewModel> TimeMarkers { get; } = new();

        /// <summary>
        /// 選中字幕項目的命令
        /// </summary>
        public DelegateCommand<SubtitleTimelineItemViewModel> SelectSubtitleCommand { get; }

        private long _totalDurationMilliseconds = 0;
        public long TotalDurationMilliseconds
        {
            get => _totalDurationMilliseconds;
            set => SetProperty(ref _totalDurationMilliseconds, value);
        }

        // 儲存原始字幕資料，用於播放時間同步
        private List<SubtitleItem> _originalSubtitles = new();
        
        // 預解析的字幕時間資料，避免重複解析
        private List<(TimeSpan StartTime, TimeSpan EndTime)> _parsedSubtitleTimes = new();

        /// <summary>
        /// 當前播放時間
        /// </summary>
        public TimeSpan CurrentTime { get; private set; } = TimeSpan.Zero;
        
        // 快取波形資料，避免重複生成
        private List<double>? _cachedWaveformData = null;
        private bool _isWaveformGenerated = false; // 標記波形是否已完整生成

        public TimelineViewModel(IEventAggregator eventAggregator, IAudioProcessingService audioService, IUndoRedoService undoRedoService)
        {
            _eventAggregator = eventAggregator;
            _audioService = audioService;
            _undoRedoService = undoRedoService;

            // 初始化命令
            SelectSubtitleCommand = new DelegateCommand<SubtitleTimelineItemViewModel>(ExecuteSelectSubtitle);

            // 訂閱影片載入事件
            _eventAggregator.GetEvent<LoadVideoEvent>().Subscribe(OnVideoLoaded);
            
            // 訂閱字幕載入事件
            _eventAggregator.GetEvent<SubtitlesReadyEvent>().Subscribe(OnSubtitlesReceived);
            
            // 訂閱影片長度變化事件
            _eventAggregator.GetEvent<VideoLengthChangedEvent>().Subscribe(OnVideoLengthChanged);
            
            // 訂閱播放時間變化事件（只更新CurrentTime屬性）
            _eventAggregator.GetEvent<PlayerTimeChangedEvent>().Subscribe(OnPlayerTimeUpdated);
            
            // 訂閱字幕更新事件
            _eventAggregator.GetEvent<SubtitleUpdatedEvent>().Subscribe(OnSubtitleUpdated);
            
            // 訂閱字幕新增和刪除事件
            _eventAggregator.GetEvent<SubtitleAddedEvent>().Subscribe(OnSubtitleAdded);
            _eventAggregator.GetEvent<SubtitleDeletedEvent>().Subscribe(OnSubtitleDeleted);
            
            // 訂閱AI處理開始事件，立即清空字幕
            _eventAggregator.GetEvent<AiProcessingStartedEvent>().Subscribe(OnAiProcessingStarted);

            System.Diagnostics.Debug.WriteLine("TimelineViewModel 已初始化");
        }

        /// <summary>
        /// 執行選中字幕命令
        /// </summary>
        /// <param name="subtitleItemVM">被點擊的字幕時間軸項目</param>
        private void ExecuteSelectSubtitle(SubtitleTimelineItemViewModel subtitleItemVM)
        {
            if (subtitleItemVM == null) return;

            try
            {
                // 根據字幕項目的顯示文字在原始字幕中找到對應的項目
                var originalSubtitle = _originalSubtitles.FirstOrDefault(s => s.Text == subtitleItemVM.DisplayText);
                
                if (originalSubtitle != null)
                {
                    // 發布選中字幕事件
                    _eventAggregator.GetEvent<SelectSubtitleEvent>().Publish(originalSubtitle);
                    System.Diagnostics.Debug.WriteLine($"Timeline 發布選中字幕事件: {originalSubtitle.Text}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"執行選中字幕命令失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新當前播放時間（唯一任務就是更新CurrentTime屬性）
        /// </summary>
        private void OnPlayerTimeUpdated(TimeSpan currentTime)
        {
            CurrentTime = currentTime;
            
            // 更新字幕項目的活動狀態
            UpdateSubtitleActiveStates(currentTime);
            
            // Debug輸出（幫助診斷問題）
            // System.Diagnostics.Debug.WriteLine($"Timeline 接收播放時間: {currentTime:mm\\:ss\\.fff}, 字幕數量: {SubtitleItems.Count}");
        }

        /// <summary>
        /// 設定可視區域寬度
        /// </summary>
        /// <param name="width">可視區域寬度</param>
        public void SetViewportWidth(double width)
        {
            ViewportWidth = width;
            System.Diagnostics.Debug.WriteLine($"Timeline 設定可視區域寬度: {width:F1}");
            
            // 重新生成時間刻度
            GenerateTimeMarkers();
            
            // 如果有快取的波形資料且尚未生成完整波形，現在生成
            if (!_isWaveformGenerated && _cachedWaveformData != null && TotalDurationMilliseconds > 0)
            {
                // 重新計算時間軸總寬度
                var totalSeconds = TotalDurationMilliseconds / 1000.0;
                var contentWidth = totalSeconds * PixelsPerSecond;
                TotalTimelineWidth = LeadingSpaceWidth + contentWidth;
                
                GenerateCompleteWaveform();
            }
            
            // 如果影片已載入但當前播放時間為 0，重新初始化播放位置
            if (TotalDurationMilliseconds > 0)
            {
                InitializePlaybackPosition();
            }
        }

        /// <summary>
        /// 處理影片載入事件
        /// </summary>
        /// <param name="videoPath">影片路徑</param>
        private async void OnVideoLoaded(string videoPath)
        {
            try
            {
                // 生成波形資料並快取
                _cachedWaveformData = await _audioService.GenerateWaveformAsync(videoPath);
                
                // 如果時間軸寬度已知，立即生成完整波形
                if (TotalTimelineWidth > 0)
                {
                    GenerateCompleteWaveform();
                }
                // 否則等待影片長度變化事件後再生成
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成波形失敗: {ex.Message}");
                
                // 如果失敗，建立一個簡單的測試波形
                _cachedWaveformData = null; // 清空快取，使用測試波形
                if (TotalTimelineWidth > 0)
                {
                    GenerateCompleteWaveform();
                }
            }
        }

        /// <summary>
        /// 處理字幕載入事件
        /// </summary>
        /// <param name="project">字幕專案資料</param>
        private void OnSubtitlesReceived(SubtitleProject project)
        {
            try
            {
                // 確保字幕項目是空的（雖然在AI開始時已經清空，但為了安全起見再次確認）
                if (SubtitleItems.Count > 0 || _originalSubtitles.Count > 0 || _parsedSubtitleTimes.Count > 0)
                {
                    ClearAllTimelineSubtitles();
                }

                // 儲存原始字幕資料
                _originalSubtitles.AddRange(project.Items);

                // 預解析所有字幕的時間，避免重複解析
                foreach (var subtitle in project.Items)
                {
                    if (TryParseSubtitleTime(subtitle.StartTime, out TimeSpan startTime) &&
                        TryParseSubtitleTime(subtitle.EndTime, out TimeSpan endTime))
                    {
                        _parsedSubtitleTimes.Add((startTime, endTime));
                    }
                    else
                    {
                        _parsedSubtitleTimes.Add((TimeSpan.Zero, TimeSpan.Zero));
                    }
                }

                // 為每個字幕項目建立對應的時間軸項目
                foreach (var subtitle in project.Items)
                {
                    var timelineItem = CreateSubtitleTimelineItem(subtitle);
                    if (timelineItem != null)
                    {
                        SubtitleItems.Add(timelineItem);
                    }
                }

                // System.Diagnostics.Debug.WriteLine($"Timeline 載入了 {SubtitleItems.Count} 個字幕項目");
                
                // 詳細輸出每個字幕項目的資訊
                // for (int i = 0; i < Math.Min(3, SubtitleItems.Count); i++)
                // {
                //     var item = SubtitleItems[i];
                //     System.Diagnostics.Debug.WriteLine($"  字幕 {i+1}: Left={item.Left:F1}, Width={item.Width:F1}, Text='{item.DisplayText}'");
                // }
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"載入字幕到 Timeline 失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 處理影片長度變化事件
        /// </summary>
        /// <param name="lengthMs">影片總長度（毫秒）</param>
        private void OnVideoLengthChanged(long lengthMs)
        {
            TotalDurationMilliseconds = lengthMs;
            
            // 根據影片總時長和固定比例計算實際內容寬度
            var totalSeconds = lengthMs / 1000.0;
            var contentWidth = totalSeconds * PixelsPerSecond;
            
            // 加上前置空白區域，讓時間 0 能顯示在畫面中央
            TotalTimelineWidth = LeadingSpaceWidth + contentWidth;
            
            // System.Diagnostics.Debug.WriteLine($"Timeline 設定總長度: {lengthMs} ms，內容寬度: {contentWidth} px，總寬度: {TotalTimelineWidth} px，前置空白: {LeadingSpaceWidth} px");
            
            // 如果波形尚未生成且已有快取資料，現在生成完整波形
            if (!_isWaveformGenerated && _cachedWaveformData != null)
            {
                GenerateCompleteWaveform();
            }

            // 重新計算所有字幕項目的位置和寬度
            RecalculateSubtitleItemPositions();
            
            // 生成時間刻度
            GenerateTimeMarkers();
            
            // 初始化播放位置到中間（確保播放頭一開始就顯示在畫面中央）
            InitializePlaybackPosition();
        }

        /// <summary>
        /// 處理字幕更新事件
        /// </summary>
        /// <param name="updatedSubtitle">已更新的字幕項目</param>
        private void OnSubtitleUpdated(SubtitleItem updatedSubtitle)
        {
            try
            {
                // 找到對應的時間軸項目並更新
                for (int i = 0; i < _originalSubtitles.Count; i++)
                {
                    if (_originalSubtitles[i] == updatedSubtitle)
                    {
                        // 更新原始字幕資料
                        _originalSubtitles[i] = updatedSubtitle;
                        
                        // 重新解析時間
                        TimeSpan startTime, endTime;
                        if (TryParseSubtitleTime(updatedSubtitle.StartTime, out startTime) &&
                            TryParseSubtitleTime(updatedSubtitle.EndTime, out endTime))
                        {
                            _parsedSubtitleTimes[i] = (startTime, endTime);
                            
                            // 更新時間軸項目的顯示文字和位置
                            if (i < SubtitleItems.Count)
                            {
                                var timelineItem = SubtitleItems[i];
                                timelineItem.DisplayText = updatedSubtitle.Text;
                                
                                // 重新計算位置和寬度
                                var leftPixels = (startTime.TotalSeconds * PixelsPerSecond);
                                var widthPixels = (endTime - startTime).TotalSeconds * PixelsPerSecond;
                                
                                timelineItem.Left = leftPixels;
                                timelineItem.Width = Math.Max(1, widthPixels);
                            }
                        }
                        else
                        {
                            _parsedSubtitleTimes[i] = (TimeSpan.Zero, TimeSpan.Zero);
                            
                            // 即使時間解析失敗，也要更新顯示文字
                            if (i < SubtitleItems.Count)
                            {
                                var timelineItem = SubtitleItems[i];
                                timelineItem.DisplayText = updatedSubtitle.Text;
                            }
                        }
                        
                        break;
                    }
                }
                
                // System.Diagnostics.Debug.WriteLine($"Timeline 已更新字幕: {updatedSubtitle.Text}");
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"更新字幕到 Timeline 失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 處理字幕新增事件
        /// </summary>
        /// <param name="newSubtitle">新增的字幕項目</param>
        private void OnSubtitleAdded(SubtitleItem newSubtitle)
        {
            try
            {
                // 將新字幕加入到原始字幕集合
                _originalSubtitles.Add(newSubtitle);
                
                // 預解析新字幕的時間
                if (TryParseSubtitleTime(newSubtitle.StartTime, out TimeSpan startTime) &&
                    TryParseSubtitleTime(newSubtitle.EndTime, out TimeSpan endTime))
                {
                    _parsedSubtitleTimes.Add((startTime, endTime));
                }
                else
                {
                    _parsedSubtitleTimes.Add((TimeSpan.Zero, TimeSpan.Zero));
                }
                
                // 建立時間軸項目並加入到顯示集合
                var timelineItem = CreateSubtitleTimelineItem(newSubtitle);
                if (timelineItem != null)
                {
                    SubtitleItems.Add(timelineItem);
                    // System.Diagnostics.Debug.WriteLine($"Timeline 新增字幕項目: {newSubtitle.Text}");
                }
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"Timeline 處理字幕新增失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 處理AI處理開始事件，立即清空時間軸中的字幕
        /// </summary>
        private void OnAiProcessingStarted()
        {
            try
            {
                // 清空時間軸中的所有字幕項目
                ClearAllTimelineSubtitles();
                
                // System.Diagnostics.Debug.WriteLine("Timeline 收到AI處理開始事件，已清空所有字幕");
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"Timeline 處理AI開始事件失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空時間軸中的所有字幕項目
        /// </summary>
        private void ClearAllTimelineSubtitles()
        {
            // 清空字幕項目集合
            SubtitleItems.Clear();
            
            // 清空原始字幕資料
            _originalSubtitles.Clear();
            
            // 清空預解析的時間資料
            _parsedSubtitleTimes.Clear();
        }

        /// <summary>
        /// 處理字幕刪除事件
        /// </summary>
        /// <param name="deletedSubtitle">被刪除的字幕項目</param>
        private void OnSubtitleDeleted(SubtitleItem deletedSubtitle)
        {
            try
            {
                // 在原始字幕集合中找到並移除對應項目
                var index = _originalSubtitles.FindIndex(s => s == deletedSubtitle || 
                    (s.Text == deletedSubtitle.Text && s.StartTime == deletedSubtitle.StartTime && s.EndTime == deletedSubtitle.EndTime));
                
                if (index >= 0)
                {
                    _originalSubtitles.RemoveAt(index);
                    
                    // 移除對應的預解析時間資料
                    if (index < _parsedSubtitleTimes.Count)
                    {
                        _parsedSubtitleTimes.RemoveAt(index);
                    }
                    
                    // 移除對應的時間軸項目
                    if (index < SubtitleItems.Count)
                    {
                        SubtitleItems.RemoveAt(index);
                        // System.Diagnostics.Debug.WriteLine($"Timeline 刪除字幕項目: {deletedSubtitle.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"Timeline 處理字幕刪除失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成時間刻度
        /// </summary>
        private void GenerateTimeMarkers()
        {
            TimeMarkers.Clear();

            if (TotalDurationMilliseconds <= 0)
                return;

            var totalSeconds = TotalDurationMilliseconds / 1000.0;
            
            // 每1秒生成一個主要刻度
            for (var seconds = 0; seconds <= totalSeconds; seconds += TimeMarkerIntervalSeconds)
            {
                var pixelPosition =  (seconds * PixelsPerSecond);
                var timeSpan = TimeSpan.FromSeconds(seconds);
                var displayTime = $"{timeSpan:mm\\:ss}";

                TimeMarkers.Add(new TimeMarkerViewModel
                {
                    Left = pixelPosition,
                    DisplayTime = displayTime
                });
            }

            System.Diagnostics.Debug.WriteLine($"Timeline 生成了 {TimeMarkers.Count} 個時間刻度");
        }

        /// <summary>
        /// 初始化播放位置，讓播放頭顯示在畫面中央
        /// </summary>
        private void InitializePlaybackPosition()
        {
            try
            {
                // 確保有足夠的可視區域寬度資訊
                if (ViewportWidth <= 0)
                {
                    // 如果尚未取得可視區域寬度，使用預設值
                    ViewportWidth = EstimatedViewportWidth;
                }

                System.Diagnostics.Debug.WriteLine($"Timeline 初始化播放位置，可視寬度: {ViewportWidth:F1}，總寬度: {TotalTimelineWidth:F1}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化播放位置失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 輔助方法：解析字幕時間字串
        /// </summary>
        /// <param name="timeString">時間字串（如 "00:01:23,456" 或 "00:01:23,4"）</param>
        /// <param name="result">解析結果</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseSubtitleTime(string timeString, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            
            // 支援多種毫秒格式
            return TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,fff", null, out result) ||  // 3位毫秒
                   TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,ff", null, out result) ||   // 2位毫秒
                   TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\,f", null, out result);      // 1位毫秒
        }

        /// <summary>
        /// 更新字幕項目的活動狀態
        /// </summary>
        /// <param name="currentTime">當前播放時間</param>
        private void UpdateSubtitleActiveStates(TimeSpan currentTime)
        {
            try
            {
                // 使用預解析的時間資料，避免重複解析
                for (int i = 0; i < SubtitleItems.Count && i < _parsedSubtitleTimes.Count; i++)
                {
                    var timelineItem = SubtitleItems[i];
                    var (startTime, endTime) = _parsedSubtitleTimes[i];

                    // 跳過無效的時間資料
                    if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
                        continue;

                    // 檢查當前時間是否在這個字幕的時間範圍內
                    bool isActive = currentTime >= startTime && currentTime <= endTime;
                    
                    // 只在狀態真正改變時才更新，減少不必要的UI更新
                    if (timelineItem.IsActive != isActive)
                    {
                        timelineItem.IsActive = isActive;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新字幕活動狀態失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 根據字幕項目建立時間軸項目
        /// </summary>
        /// <param name="subtitle">字幕項目</param>
        /// <returns>時間軸字幕項目</returns>
        private SubtitleTimelineItemViewModel? CreateSubtitleTimelineItem(SubtitleItem subtitle)
        {
            try
            {
                // 使用輔助方法解析時間
                if (!TryParseSubtitleTime(subtitle.StartTime, out TimeSpan startTime))
                {
                    System.Diagnostics.Debug.WriteLine($"無法解析開始時間: {subtitle.StartTime}");
                    return null;
                }

                if (!TryParseSubtitleTime(subtitle.EndTime, out TimeSpan endTime))
                {
                    System.Diagnostics.Debug.WriteLine($"無法解析結束時間: {subtitle.EndTime}");
                    return null;
                }

                // 計算像素位置和寬度（加上前置空白區域的偏移）
                var leftPixels = (startTime.TotalSeconds * PixelsPerSecond);
                var widthPixels = (endTime - startTime).TotalSeconds * PixelsPerSecond;

                // 建立新的 SubtitleTimelineItemViewModel 實例
                var viewModel = new SubtitleTimelineItemViewModel(subtitle, _undoRedoService)
                {
                    Left = leftPixels,
                    Width = Math.Max(1, widthPixels), // 最小寬度為1像素
                    DisplayText = subtitle.Text,
                    IsActive = subtitle.IsActive
                };

                // 設定像素比例
                viewModel.SetPixelsPerSecond(PixelsPerSecond);

                return viewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"建立時間軸字幕項目失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重新計算所有字幕項目的位置和寬度
        /// </summary>
        private void RecalculateSubtitleItemPositions()
        {
            if (_originalSubtitles.Count != SubtitleItems.Count)
                return;

            for (int i = 0; i < _originalSubtitles.Count; i++)
            {
                var originalSubtitle = _originalSubtitles[i];
                var timelineItem = SubtitleItems[i];

                // 使用預解析的時間資料重新計算位置和寬度
                if (i < _parsedSubtitleTimes.Count)
                {
                    var (startTime, endTime) = _parsedSubtitleTimes[i];
                    
                    if (startTime != TimeSpan.Zero || endTime != TimeSpan.Zero)
                    {
                        var leftPixels =  (startTime.TotalSeconds * PixelsPerSecond);
                        var widthPixels = (endTime - startTime).TotalSeconds * PixelsPerSecond;

                        timelineItem.Left = leftPixels;
                        timelineItem.Width = Math.Max(1, widthPixels);
                    }
                }
            }
        }

        /// <summary>
        /// 根據波形資料生成波形點（一次性生成完整波形）
        /// </summary>
        /// <param name="waveformData">波形資料</param>
        private void GenerateWaveformPoints(List<double> waveformData)
        {
            var points = new PointCollection();
            var waveformHeight = 50.0; // 波形圖高度（縮小以留空間給字幕）
            var waveformTopOffset = 5.0; // 波形距離頂部的偏移
            var amplitudeMultiplier = 5; // 幅度放大倍數，讓波形更明顯
            
            // 計算實際內容區域的寬度（排除前置空白區域）
            var contentWidth = TotalTimelineWidth - LeadingSpaceWidth;
            
            if (waveformData.Count > 0 && contentWidth > 0)
            {
                // 根據時間軸總寬度決定波形點密度，確保波形覆蓋整個時間軸
                // 每10像素一個點，但限制在200-4000個點之間
                var targetPoints = Math.Max(200, Math.Min(4000, (int)(contentWidth / 10.0)));
                var actualPoints = Math.Min(targetPoints, waveformData.Count);
                var step = waveformData.Count / (double)actualPoints;
                
                for (int i = 0; i < actualPoints; i++)
                {
                    var dataIndex = (int)(i * step);
                    if (dataIndex >= waveformData.Count) dataIndex = waveformData.Count - 1;
                    
                    // 波形圖從前置空白區域的結尾開始，均勻分布在整個內容區域
                    var x = ((double)i / actualPoints * contentWidth);
                    
                    // 放大波形幅度，但確保不超出範圍
                    var amplifiedValue = Math.Min(1.0, waveformData[dataIndex] * amplitudeMultiplier);
                    // 將波形移到上半部：從頂部偏移開始，向下延伸
                    var y = waveformTopOffset + (waveformHeight - (amplifiedValue * waveformHeight));
                    points.Add(new System.Windows.Point(x, y));
                }
            }

            WaveformPoints = points;
            System.Diagnostics.Debug.WriteLine($"Timeline 一次性生成了 {points.Count} 個波形點，覆蓋寬度: {contentWidth:F1}px，位置：上半部區域，幅度放大: {amplitudeMultiplier}x");
        }

        /// <summary>
        /// 生成測試波形（一次性生成完整測試波形）
        /// </summary>
        private void GenerateTestWaveform()
        {
            var testPoints = new PointCollection();
            var waveformHeight = 50.0; // 波形圖高度（縮小以留空間給字幕）
            var waveformTopOffset = 10.0; // 波形距離頂部的偏移
            var amplitudeMultiplier = 1.8; // 測試波形的幅度倍數
            
            // 計算實際內容區域的寬度（排除前置空白區域）
            var contentWidth = TotalTimelineWidth - LeadingSpaceWidth;
            
            if (contentWidth > 0)
            {
                // 根據時間軸寬度生成適量的測試點，每15像素一個點，限制在100-500個點之間
                var pointCount = Math.Max(100, Math.Min(500, (int)(contentWidth / 15.0)));
                
                for (int i = 0; i < pointCount; i++)
                {
                    // 波形圖從前置空白區域的結尾開始，均勻分布
                    var x = ((double)i / pointCount * contentWidth);
                    // 增加測試波形的幅度，讓它更明顯
                    var y = waveformTopOffset + waveformHeight / 2 + Math.Sin(i * 0.1) * waveformHeight / 4 * amplitudeMultiplier;
                    testPoints.Add(new System.Windows.Point(x, y));
                }
            }
            
            WaveformPoints = testPoints;
            // System.Diagnostics.Debug.WriteLine($"Timeline 一次性生成了 {testPoints.Count} 個測試波形點，覆蓋寬度: {contentWidth:F1}px");
        }

        /// <summary>
        /// 生成完整的波形圖（一次性生成，之後不再重新生成）
        /// </summary>
        private void GenerateCompleteWaveform()
        {
            if (_isWaveformGenerated)
                return; // 已經生成過，直接返回

            try
            {
                // 如果有快取的原始波形資料，使用它生成完整波形
                if (_cachedWaveformData != null && _cachedWaveformData.Count > 0)
                {
                    GenerateWaveformPoints(_cachedWaveformData);
                    System.Diagnostics.Debug.WriteLine("Timeline 使用真實音訊資料生成完整波形");
                }
                // 否則生成測試波形
                else
                {
                    GenerateTestWaveform();
                    System.Diagnostics.Debug.WriteLine("Timeline 使用測試資料生成完整波形");
                }
                
                _isWaveformGenerated = true; // 標記為已生成
                System.Diagnostics.Debug.WriteLine("Timeline 波形生成完成，後續不會再重新生成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成完整波形失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 釋放資源
        /// </summary>
        public void Dispose()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TimelineViewModel 已釋放資源");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimelineViewModel Dispose 失敗: {ex.Message}");
            }
        }
    }
} 