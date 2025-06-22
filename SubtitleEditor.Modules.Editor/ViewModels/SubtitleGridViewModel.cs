using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using Prism.Events;
using Prism.Mvvm;
using Prism.Commands;
using Prism.Dialogs;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;
using SubtitleEditor.Common.Enums;
using SubtitleEditor.Modules.Editor.Commands;
using SubtitleEditor.Core.Services;
using GongSolutions.Wpf.DragDrop;

namespace SubtitleEditor.Modules.Editor.ViewModels
{
    public class SubtitleGridViewModel : BindableBase, IDropTarget
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ISubtitleWriterService _writerService;
        private readonly IUndoRedoService _undoRedoService;
        private readonly IDialogService _dialogService;
        private readonly IAiServiceFactory _aiServiceFactory;
        private string _currentFilePath = string.Empty;
        private string _currentVideoPath = string.Empty;
        private SubtitleItem? _activeSubtitle;
        private string? _textBeforeEdit;

        private bool _isEditingEnabled = true;
        public bool IsEditingEnabled
        {
            get => _isEditingEnabled;
            set
            {
                SetProperty(ref _isEditingEnabled, value);
                // 更新所有相關命令的可用性
                AddSubtitleCommand.RaiseCanExecuteChanged();
                GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
                GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isVideoLoaded = false;
        public bool IsVideoLoaded
        {
            get => _isVideoLoaded;
            set
            {
                SetProperty(ref _isVideoLoaded, value);
                GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
                GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isAiProcessing = false;
        public bool IsAiProcessing
        {
            get => _isAiProcessing;
            set
            {
                SetProperty(ref _isAiProcessing, value);
                GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
                GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
            }
        }

        // 新增屬性：控制按鈕可用性
        public bool CanUseLocalAi => IsVideoLoaded && IsEditingEnabled && !IsAiProcessing;
        public bool CanUseCloudAi => IsVideoLoaded && IsEditingEnabled && !IsAiProcessing;

        private int _aiProgressPercentage = 0;
        public int AiProgressPercentage
        {
            get => _aiProgressPercentage;
            set => SetProperty(ref _aiProgressPercentage, value);
        }

        private string _aiProgressMessage = string.Empty;
        public string AiProgressMessage
        {
            get => _aiProgressMessage;
            set => SetProperty(ref _aiProgressMessage, value);
        }

        private bool _isDownloadProgress = false;
        public bool IsDownloadProgress
        {
            get => _isDownloadProgress;
            set => SetProperty(ref _isDownloadProgress, value);
        }

        public ObservableCollection<SubtitleItem> Subtitles { get; set; }

        private SubtitleItem _selectedSubtitle;
        public SubtitleItem SelectedSubtitle
        {
            get { return _selectedSubtitle; }
            set 
            { 
                // 過濾掉 NewItemPlaceholder 和其他非 SubtitleItem 物件
                if (value != null && !(value is SubtitleItem))
                {
                    return; // 忽略非 SubtitleItem 物件（如 NewItemPlaceholder）
                }
                
                SetProperty(ref _selectedSubtitle, value);
                
                // 當選中字幕項目時，發布影片跳轉事件
                if (value != null)
                {
                    // 嘗試將 StartTime 字串轉換為 TimeSpan
                    if (TimeSpan.TryParseExact(value.StartTime, @"hh\:mm\:ss\,f", null, out TimeSpan timeSpan))
                    {
                        _eventAggregator.GetEvent<SeekVideoEvent>().Publish(timeSpan);
                    }
                }
            }
        }

        public DelegateCommand AddSubtitleCommand { get; }
        public DelegateCommand<object> DeleteSubtitleCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand<DataGridBeginningEditEventArgs> BeginningEditCommand { get; }
        public DelegateCommand<DataGridCellEditEndingEventArgs> CellEditEndingCommand { get; }
        public DelegateCommand GenerateLocalAiSubtitlesCommand { get; }
        public DelegateCommand GenerateCloudAiSubtitlesCommand { get; }
        
        // 從服務中暴露 Undo/Redo 命令
        public DelegateCommand UndoCommand => _undoRedoService.UndoCommand;
        public DelegateCommand RedoCommand => _undoRedoService.RedoCommand;

        public SubtitleGridViewModel(IEventAggregator eventAggregator, ISubtitleWriterService writerService, IUndoRedoService undoRedoService, IDialogService dialogService, IAiServiceFactory aiServiceFactory)
        {
            _eventAggregator = eventAggregator;
            _writerService = writerService;
            _undoRedoService = undoRedoService;
            _dialogService = dialogService;
            _aiServiceFactory = aiServiceFactory;
            
            // 初始化命令
            AddSubtitleCommand = new DelegateCommand(ExecuteAddSubtitle, CanExecuteAddSubtitle);
            DeleteSubtitleCommand = new DelegateCommand<object>(ExecuteDeleteSubtitle);
            SaveCommand = new DelegateCommand(ExecuteSave, CanExecuteSave);
            BeginningEditCommand = new DelegateCommand<DataGridBeginningEditEventArgs>(OnBeginningEdit);
            CellEditEndingCommand = new DelegateCommand<DataGridCellEditEndingEventArgs>(OnCellEditEnding);
            GenerateLocalAiSubtitlesCommand = new DelegateCommand(ExecuteGenerateLocalAiSubtitles, CanExecuteGenerateLocalAiSubtitles);
            GenerateCloudAiSubtitlesCommand = new DelegateCommand(ExecuteGenerateCloudAiSubtitles, CanExecuteGenerateCloudAiSubtitles);
            
            InitializeSubtitles();
            SubscribeToEvents();
        }

        private void InitializeSubtitles()
        {
            Subtitles = new ObservableCollection<SubtitleItem>
            {
                new SubtitleItem
                {
                    Index = 1,
                    StartTime = "00:00:00,0",
                    EndTime = "00:00:03,5",
                    Speaker = "Speaker1",
                    Text = "歡迎使用字幕神器！"
                },
                new SubtitleItem
                {
                    Index = 2,
                    StartTime = "00:00:04,0",
                    EndTime = "00:00:07,2",
                    Speaker = "Speaker1",
                    Text = "這是一個強大的字幕編輯工具。"
                },
                new SubtitleItem
                {
                    Index = 3,
                    StartTime = "00:00:08,0",
                    EndTime = "00:00:12,0",
                    Speaker = "Speaker1",
                    Text = "您可以輕鬆編輯和管理字幕檔案。"
                }
            };
            
            // 監聽集合變化，當有新項目加入時自動設定序號
            Subtitles.CollectionChanged += OnSubtitlesCollectionChanged;
        }

        private void SubscribeToEvents()
        {
            _eventAggregator.GetEvent<SubtitlesReadyEvent>().Subscribe(OnSubtitlesReceived);
            _eventAggregator.GetEvent<SaveSubtitleEvent>().Subscribe(OnSaveSubtitle);
            _eventAggregator.GetEvent<SaveSubtitleWithOptionsEvent>().Subscribe(OnSaveSubtitleWithOptions);
            _eventAggregator.GetEvent<PlayerTimeChangedEvent>().Subscribe(OnPlayerTimeChanged);
            _eventAggregator.GetEvent<VideoLoadedEvent>().Subscribe(OnVideoLoaded);
            _eventAggregator.GetEvent<AiProcessingStartedEvent>().Subscribe(OnAiProcessingStarted);
            _eventAggregator.GetEvent<AiProcessingFinishedEvent>().Subscribe(OnAiProcessingFinished);
            _eventAggregator.GetEvent<AiProgressUpdatedEvent>().Subscribe(OnAiProgressUpdated);
            _eventAggregator.GetEvent<SelectSubtitleEvent>().Subscribe(OnSubtitleSelected);
            _eventAggregator.GetEvent<SubtitleUpdatedEvent>().Subscribe(OnSubtitleUpdated);
        }

        private void OnSubtitlesReceived(SubtitleProject project)
        {
            // 暫時移除事件監聽，避免在加入字幕時觸發不必要的事件
            Subtitles.CollectionChanged -= OnSubtitlesCollectionChanged;
            
            try
            {
                // 儲存檔案路徑
                _currentFilePath = project.FilePath;
                
                // 確保字幕集合是空的（雖然在AI開始時已經清空，但為了安全起見再次確認）
                if (Subtitles.Count > 0)
                {
                    Subtitles.Clear();
                }

                // 將解析後的字幕資料加入到集合中
                foreach (var item in project.Items)
                {
                    Subtitles.Add(item);
                }
            }
            finally
            {
                // 重新加入事件監聽
                Subtitles.CollectionChanged += OnSubtitlesCollectionChanged;
                
                // 更新儲存命令的可用性
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private void OnSaveSubtitle(string filePath)
        {
            try
            {
                _writerService.Write(filePath, Subtitles);
            }
            catch (System.Exception ex)
            {
                // 可以在這裡處理錯誤，或者讓上層處理
                throw new System.InvalidOperationException($"儲存字幕檔案時發生錯誤：{ex.Message}", ex);
            }
        }

        private void OnSaveSubtitleWithOptions(SaveSubtitleOptions options)
        {
            try
            {
                _writerService.Write(options.FilePath, Subtitles, options.IncludeSpeaker);
            }
            catch (System.Exception ex)
            {
                // 可以在這裡處理錯誤，或者讓上層處理
                throw new System.InvalidOperationException($"儲存字幕檔案時發生錯誤：{ex.Message}", ex);
            }
        }

        private void ExecuteSave()
        {
            // 如果沒有檔案路徑，執行另存新檔
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                ExecuteSaveAs();
                return;
            }

            try
            {
                _writerService.Write(_currentFilePath, Subtitles);
                System.Windows.MessageBox.Show("字幕檔案儲存成功！", "儲存完成", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"儲存字幕檔案時發生錯誤：\n{ex.Message}", "錯誤", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSave()
        {
            // 有字幕資料就可以存檔（不管是否有檔案路徑）
            return Subtitles.Count > 0 && IsEditingEnabled && !IsAiProcessing;
        }

        private void ExecuteSaveAs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "字幕檔案 (*.srt)|*.srt|All files (*.*)|*.*",
                Title = "儲存字幕檔案",
                DefaultExt = "srt"
            };

            // 如果有當前影片路徑，自動設定同名字幕檔案
            if (!string.IsNullOrEmpty(_currentVideoPath))
            {
                var videoFileName = System.IO.Path.GetFileNameWithoutExtension(_currentVideoPath);
                var videoDirectory = System.IO.Path.GetDirectoryName(_currentVideoPath);
                dialog.FileName = videoFileName;
                dialog.InitialDirectory = videoDirectory;
            }

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 詢問是否包含演講者
                    var result = System.Windows.MessageBox.Show("是否在儲存的字幕檔案中包含演講者資訊？", 
                                                        "儲存選項", 
                                                        System.Windows.MessageBoxButton.YesNo, 
                                                        System.Windows.MessageBoxImage.Question);
                    
                    bool includeSpeaker = result == System.Windows.MessageBoxResult.Yes;
                    
                    _writerService.Write(dialog.FileName, Subtitles, includeSpeaker);
                    
                    // 儲存成功後，更新當前檔案路徑
                    _currentFilePath = dialog.FileName;
                    
                    System.Windows.MessageBox.Show("字幕檔案儲存成功！", "儲存完成", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"儲存字幕檔案時發生錯誤：\n{ex.Message}", "錯誤", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteAddSubtitle()
        {
            var newSubtitle = new SubtitleItem
            {
                Index = Subtitles.Count + 1,
                StartTime = "00:00:00,0",
                EndTime = "00:00:00,0",
                Speaker = "Speaker1",
                Text = ""
            };

            // 決定插入位置
            int insertIndex = -1;
            if (SelectedSubtitle != null)
            {
                var selectedIndex = Subtitles.IndexOf(SelectedSubtitle);
                if (selectedIndex >= 0 && selectedIndex < Subtitles.Count - 1)
                {
                    insertIndex = selectedIndex + 1;
                }
            }

            // 建立可復原的新增命令並執行
            var addCommand = new AddSubtitleCommand(Subtitles, newSubtitle, insertIndex);
            _undoRedoService.Do(addCommand);

            // 重新編號
            UpdateSubtitleIndexes();
        }

        private bool CanExecuteAddSubtitle()
        {
            return IsEditingEnabled && !IsAiProcessing;
        }

        private void ExecuteDeleteSubtitle(object parameter)
        {
            // 安全地將傳入的 parameter 轉換為 SubtitleItem
            if (parameter is SubtitleItem subtitle)
            {
                try
                {
                    // 建立可復原的刪除命令並執行
                    var deleteCommand = new DeleteSubtitleCommand(Subtitles, subtitle);
                    _undoRedoService.Do(deleteCommand);

                    // 重新編號
                    UpdateSubtitleIndexes();
                }
                catch (ArgumentException ex)
                {
                    // 如果項目不存在於集合中，顯示錯誤訊息
                    System.Windows.MessageBox.Show($"刪除字幕時發生錯誤：\n{ex.Message}", "錯誤", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateSubtitleIndexes()
        {
            for (int i = 0; i < Subtitles.Count; i++)
            {
                Subtitles[i].Index = i + 1;
            }
        }

        private void OnBeginningEdit(DataGridBeginningEditEventArgs e)
        {
            // 取得正在編輯的儲存格中的項目
            if (e.Row.Item is SubtitleItem subtitleItem)
            {
                // 根據編輯的欄位儲存對應的原始值
                if (e.Column.Header?.ToString() == "字幕文字")
                {
                    _textBeforeEdit = subtitleItem.Text;
                }
                else if (e.Column.Header?.ToString() == "演講者")
                {
                    _textBeforeEdit = subtitleItem.Speaker;
                }
                else if (e.Column.Header?.ToString() == "開始時間")
                {
                    _textBeforeEdit = subtitleItem.StartTime;
                }
                else if (e.Column.Header?.ToString() == "結束時間")
                {
                    _textBeforeEdit = subtitleItem.EndTime;
                }
            }
        }

        private void OnCellEditEnding(DataGridCellEditEndingEventArgs e)
        {
            // 只有在編輯完成時才處理（不是取消編輯）
            if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is SubtitleItem subtitleItem)
            {
                // 取得編輯後的新文字
                string newText = string.Empty;
                if (e.EditingElement is TextBox textBox)
                {
                    newText = textBox.Text ?? string.Empty;
                }

                // 檢查新舊文字是否真的有改變
                if (!string.IsNullOrEmpty(_textBeforeEdit) && _textBeforeEdit != newText)
                {
                    // 根據編輯的欄位建立對應的命令
                    IUndoableCommand? command = null;
                    
                    switch (e.Column.Header?.ToString())
                    {
                        case "字幕文字":
                            command = new EditTextCommand(subtitleItem, _textBeforeEdit, newText);
                            break;
                        case "演講者":
                            command = new EditSpeakerCommand(subtitleItem, _textBeforeEdit, newText);
                            break;
                        case "開始時間":
                            command = new EditStartTimeCommand(subtitleItem, _textBeforeEdit, newText);
                            break;
                        case "結束時間":
                            command = new EditEndTimeCommand(subtitleItem, _textBeforeEdit, newText);
                            break;
                    }

                    // 執行命令
                    if (command != null)
                    {
                        _undoRedoService.Do(command);
                    }
                }

                // 清除暫存的編輯前文字
                _textBeforeEdit = null;
            }
        }

        private void OnSubtitlesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 當有新項目加入時，自動設定正確的序號
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (SubtitleItem newItem in e.NewItems)
                {
                    // 如果新項目的 Index 是 0（預設值），則設定正確的序號
                    if (newItem.Index == 0)
                    {
                        newItem.Index = Subtitles.IndexOf(newItem) + 1;
                    }
                    
                    // 發布字幕新增事件，通知時間軸
                    _eventAggregator.GetEvent<SubtitleAddedEvent>().Publish(newItem);
                    // System.Diagnostics.Debug.WriteLine($"字幕編輯區發布新增事件: {newItem.Text}");
                }
                
                // 重新編號所有項目以確保序號連續
                UpdateSubtitleIndexes();
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (SubtitleItem deletedItem in e.OldItems)
                {
                    // 發布字幕刪除事件，通知時間軸
                    _eventAggregator.GetEvent<SubtitleDeletedEvent>().Publish(deletedItem);
                    // System.Diagnostics.Debug.WriteLine($"字幕編輯區發布刪除事件: {deletedItem.Text}");
                }
                
                // 項目被移除時重新編號
                UpdateSubtitleIndexes();
            }
        }

        private void OnPlayerTimeChanged(TimeSpan currentTime)
        {
            // 遍歷字幕集合，找到當前時間對應的字幕
            SubtitleItem? newActiveSubtitle = null;
            
            foreach (var subtitle in Subtitles)
            {
                // 嘗試解析開始時間和結束時間
                if (TimeSpan.TryParseExact(subtitle.StartTime, @"hh\:mm\:ss\,f", null, out TimeSpan startTime) &&
                    TimeSpan.TryParseExact(subtitle.EndTime, @"hh\:mm\:ss\,f", null, out TimeSpan endTime))
                {
                    // 檢查當前時間是否在這個字幕的時間範圍內
                    if (currentTime >= startTime && currentTime <= endTime)
                    {
                        newActiveSubtitle = subtitle;
                        break;
                    }
                }
            }
            
            // 如果找到的活動字幕與目前不同，更新狀態
            if (newActiveSubtitle != _activeSubtitle)
            {
                // 清除舊的活動狀態
                if (_activeSubtitle != null)
                {
                    _activeSubtitle.IsActive = false;
                }
                
                // 設定新的活動狀態
                if (newActiveSubtitle != null)
                {
                    newActiveSubtitle.IsActive = true;
                    
                    // 發布捲動事件，讓 DataGrid 自動捲動到當前活動的字幕項目
                    _eventAggregator.GetEvent<ScrollToItemEvent>().Publish(newActiveSubtitle);
                }
                
                // 更新引用
                _activeSubtitle = newActiveSubtitle;
            }
        }

        // IDropTarget 介面實作
        public void DragOver(IDropInfo dropInfo)
        {
            // 使用預設的拖曳行為
            DragDrop.DefaultDropHandler.DragOver(dropInfo);
        }

        public void Drop(IDropInfo dropInfo)
        {
            // 取得拖放的相關資訊
            if (dropInfo.Data is SubtitleItem sourceItem && 
                dropInfo.TargetCollection == Subtitles)
            {
                // 取得原始索引和新索引
                int oldIndex = Subtitles.IndexOf(sourceItem);
                int newIndex = dropInfo.InsertIndex;

                // 調整新索引（如果拖放到同一位置或無效位置，則不執行）
                if (newIndex > oldIndex)
                {
                    newIndex--; // 因為移除原項目後，後面的索引會減1
                }

                // 確保索引有效且確實有變化
                if (oldIndex >= 0 && newIndex >= 0 && 
                    newIndex < Subtitles.Count && oldIndex != newIndex)
                {
                    try
                    {
                        // 建立可復原的重新排序命令並執行
                        var reorderCommand = new ReorderCommand(Subtitles, oldIndex, newIndex);
                        _undoRedoService.Do(reorderCommand);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        // 如果索引超出範圍，顯示錯誤訊息
                        System.Windows.MessageBox.Show($"拖放排序時發生錯誤：\n{ex.Message}", "錯誤", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }
            }
        }

        private bool CanExecuteGenerateLocalAiSubtitles()
        {
            return IsVideoLoaded && IsEditingEnabled && !IsAiProcessing;
        }

        private void ExecuteGenerateLocalAiSubtitles()
        {
            // 顯示本地服務設定對話框
            var dialogParameters = new DialogParameters();
            dialogParameters.Add("ForceLocalService", true); // 強制本地服務模式

            _dialogService.ShowDialog("AiSettingsView", dialogParameters, result => 
            { 
                if (result.Result == ButtonResult.OK)
                {
                    // 呼叫非同步轉錄方法
                    StartTranscription(result);
                }
            });
        }

        private bool CanExecuteGenerateCloudAiSubtitles()
        {
            return IsVideoLoaded && IsEditingEnabled && !IsAiProcessing;
        }

        private void ExecuteGenerateCloudAiSubtitles()
        {
            // 顯示雲端服務設定對話框
            var dialogParameters = new DialogParameters();
            dialogParameters.Add("ForceCloudService", true); // 強制雲端服務模式

            _dialogService.ShowDialog("AiSettingsView", dialogParameters, result => 
            { 
                if (result.Result == ButtonResult.OK)
                {
                    // 呼叫非同步轉錄方法
                    StartTranscription(result);
                }
            });
        }

        private async void StartLocalTranscription()
        {
            // 發布 AI 處理開始事件
            _eventAggregator.GetEvent<AiProcessingStartedEvent>().Publish();

            // 在開始AI處理前先清空所有字幕，確保執行順序
            ClearAllSubtitles();

            try
            {
                // 使用預設的本地服務設定
                var serviceType = AiServiceType.Local;
                var language = "繁體中文";
                var generationMode = "小型模型 (base)";

                // 創建進度報告器
                var progressReporter = new Progress<AiProgressInfo>(progressInfo =>
                {
                    _eventAggregator.GetEvent<AiProgressUpdatedEvent>().Publish(progressInfo);
                });

                // 將舊版參數轉換為新版 API
                var modelName = MapGenerationModeToModelName(generationMode);
                
                // 使用工廠取得本地 AI 服務
                var aiService = _aiServiceFactory.GetService(serviceType);
                
                // 呼叫 AI 轉錄服務，傳遞進度報告器
                var subtitleItems = await aiService.TranscribeAsync(
                    _currentVideoPath, modelName, language, progressReporter);

                // 建立 SubtitleProject 並發布事件
                // AI 生成的字幕沒有檔案路徑，傳遞空字串以觸發另存新檔
                var project = new SubtitleProject(string.Empty, subtitleItems);
                _eventAggregator.GetEvent<SubtitlesReadyEvent>().Publish(project);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"本地 AI 字幕生成時發生錯誤：\n{ex.Message}", "錯誤", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                // 發布 AI 處理完成事件
                _eventAggregator.GetEvent<AiProcessingFinishedEvent>().Publish();
            }
        }

        private async void StartTranscription(IDialogResult dialogResult)
        {
            // 發布 AI 處理開始事件
            _eventAggregator.GetEvent<AiProcessingStartedEvent>().Publish();

            // 在開始AI處理前先清空所有字幕，確保執行順序
            ClearAllSubtitles();

            try
            {
                // 從對話方塊結果中取得設定
                var serviceType = dialogResult.Parameters.GetValue<AiServiceType>("ServiceType");
                var language = dialogResult.Parameters.GetValue<string>("Language");
                var generationMode = dialogResult.Parameters.GetValue<string>("GenerationMode");
                var segmentationMode = dialogResult.Parameters.GetValue<string>("SegmentationMode");
                var apiKey = dialogResult.Parameters.GetValue<string>("ApiKey");

                // 創建進度報告器
                var progressReporter = new Progress<AiProgressInfo>(progressInfo =>
                {
                    _eventAggregator.GetEvent<AiProgressUpdatedEvent>().Publish(progressInfo);
                });

                // 將舊版參數轉換為新版 API
                var modelName = MapGenerationModeToModelName(generationMode);
                
                // 使用工廠取得用戶選擇的 AI 服務
                var aiService = _aiServiceFactory.GetService(serviceType);
                
                // 如果是雲端服務且提供了 API Key，直接設定到服務中
                if (serviceType == AiServiceType.Cloud && !string.IsNullOrWhiteSpace(apiKey))
                {
                    // System.Diagnostics.Debug.WriteLine($"[DEBUG] 雲端服務 API Key: {apiKey?.Substring(0, Math.Min(10, apiKey.Length))}...");
                    if (aiService is WhisperApiService whisperApiService)
                    {
                        // System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功轉換為 WhisperApiService，設定 API Key");
                        whisperApiService.SetTemporaryApiKey(apiKey);
                    }
                    else
                    {
                        // System.Diagnostics.Debug.WriteLine($"[DEBUG] 轉換失敗，服務類型: {aiService?.GetType().Name}");
                    }
                }
                else
                {
                    // System.Diagnostics.Debug.WriteLine($"[DEBUG] 服務類型: {serviceType}, API Key 是否為空: {string.IsNullOrWhiteSpace(apiKey)}");
                }
                
                // 呼叫 AI 轉錄服務，傳遞進度報告器
                var subtitleItems = await aiService.TranscribeAsync(
                    _currentVideoPath, modelName, language, progressReporter);

                // 建立 SubtitleProject 並發布事件
                // AI 生成的字幕沒有檔案路徑，傳遞空字串以觸發另存新檔
                var project = new SubtitleProject(string.Empty, subtitleItems);
                _eventAggregator.GetEvent<SubtitlesReadyEvent>().Publish(project);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"AI 字幕生成時發生錯誤：\n{ex.Message}", "錯誤", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                // 發布 AI 處理完成事件
                _eventAggregator.GetEvent<AiProcessingFinishedEvent>().Publish();
            }
        }

        private void OnVideoLoaded(string videoPath)
        {
            _currentVideoPath = videoPath;
            IsVideoLoaded = !string.IsNullOrEmpty(videoPath);
            
            // 通知AI命令可用性變更
            GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
            GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanUseLocalAi));
            RaisePropertyChanged(nameof(CanUseCloudAi));
        }

        private void OnAiProcessingStarted()
        {
            IsEditingEnabled = false;
            IsAiProcessing = true;
            AiProgressPercentage = 0;
            AiProgressMessage = "準備中...";
            
            // 通知按鈕可用性變更
            GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
            GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanUseLocalAi));
            RaisePropertyChanged(nameof(CanUseCloudAi));
        }

        private void OnAiProcessingFinished()
        {
            IsEditingEnabled = true;
            IsAiProcessing = false;
            AiProgressPercentage = 0;
            AiProgressMessage = string.Empty;
            IsDownloadProgress = false;
            
            // 通知按鈕可用性變更
            GenerateLocalAiSubtitlesCommand.RaiseCanExecuteChanged();
            GenerateCloudAiSubtitlesCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanUseLocalAi));
            RaisePropertyChanged(nameof(CanUseCloudAi));
        }

        private void OnAiProgressUpdated(AiProgressInfo progressInfo)
        {
            AiProgressPercentage = progressInfo.ProgressPercentage;
            AiProgressMessage = progressInfo.StatusMessage;
            IsDownloadProgress = progressInfo.IsDownloadProgress;
        }



        /// <summary>
        /// 清空所有字幕，確保執行順序避免bug
        /// </summary>
        private void ClearAllSubtitles()
        {
            // 暫時移除事件監聽，避免在清空時觸發不必要的事件
            Subtitles.CollectionChanged -= OnSubtitlesCollectionChanged;
            
            try
            {
                // 清空現有字幕
                Subtitles.Clear();
                
                // 清空當前檔案路徑，因為字幕已被清空
                _currentFilePath = string.Empty;
                
                // 清空選中的字幕
                SelectedSubtitle = null;
            }
            finally
            {
                // 重新加入事件監聽
                Subtitles.CollectionChanged += OnSubtitlesCollectionChanged;
                
                // 更新儲存命令的可用性
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 將舊的生成模式對應到新的模型名稱
        /// </summary>
        /// <param name="generationMode">生成模式</param>
        /// <returns>模型名稱</returns>
        private static string MapGenerationModeToModelName(string generationMode)
        {
            return generationMode switch
            {
                "輕型模型 (tiny)" => "tiny",
                "小型模型 (base)" => "base",
                "中型模型 (medium)" => "medium",
                "大型模型 (large)" => "large-v3",
                _ => "base"
            };
        }

        /// <summary>
        /// 處理從Timeline選中字幕的事件
        /// </summary>
        /// <param name="item">被選中的字幕項目</param>
        private void OnSubtitleSelected(SubtitleItem item)
        {
            this.SelectedSubtitle = item;
        }

        /// <summary>
        /// 處理字幕更新事件（主要用於Timeline拖動後的同步）
        /// </summary>
        /// <param name="updatedItem">已更新的字幕項目</param>
        private void OnSubtitleUpdated(SubtitleItem updatedItem)
        {
            try
            {
                // 在 Subtitles 集合中找到對應的項目
                var existingItem = Subtitles.FirstOrDefault(s => s == updatedItem);
                
                if (existingItem != null)
                {
                    // 找到項目在集合中的索引
                    var index = Subtitles.IndexOf(existingItem);
                    
                    // System.Diagnostics.Debug.WriteLine($"字幕編輯區收到更新事件: 索引={index}, 文字='{updatedItem.Text}', 時間=[{updatedItem.StartTime} - {updatedItem.EndTime}]");
                    
                    // 由於 SubtitleItem 繼承自 BindableBase，屬性變更應該自動觸發 UI 更新
                    // 檢查時間是否真的發生了變化
                    // System.Diagnostics.Debug.WriteLine($"實際項目時間: [{existingItem.StartTime} - {existingItem.EndTime}]");
                    
                    // 如果這是當前選中的項目，確保它仍然被選中並刷新
                    if (SelectedSubtitle == updatedItem)
                    {
                        // System.Diagnostics.Debug.WriteLine($"刷新選中項目的UI顯示");
                        // 重新設定選中項目，這會觸發 UI 刷新
                        var temp = SelectedSubtitle;
                        SelectedSubtitle = null;
                        SelectedSubtitle = temp;
                    }
                }
                else
                {
                    // System.Diagnostics.Debug.WriteLine($"字幕編輯區未找到對應的更新項目: {updatedItem.Text}");
                    
                    // 輸出當前集合中的所有項目進行調試
                    // System.Diagnostics.Debug.WriteLine($"當前集合中有 {Subtitles.Count} 個項目:");
                    // for (int i = 0; i < Math.Min(3, Subtitles.Count); i++)
                    // {
                    //     var item = Subtitles[i];
                    //     System.Diagnostics.Debug.WriteLine($"  [{i}]: '{item.Text}' [{item.StartTime} - {item.EndTime}]");
                    // }
                }
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine($"處理字幕更新事件失敗: {ex.Message}");
            }
        }
    }
} 