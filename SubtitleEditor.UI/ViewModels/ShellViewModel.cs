using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace SubtitleEditor.UI.ViewModels
{
    public class ShellViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ISubtitleParserService _parserService;
        private readonly IUndoRedoService _undoRedoService;

        private bool _isAiProcessing = false;
        public bool IsAiProcessing
        {
            get => _isAiProcessing;
            set => SetProperty(ref _isAiProcessing, value);
        }

        public DelegateCommand OpenSubtitleFileCommand { get; }
        public DelegateCommand OpenVideoFileCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand SaveSubtitleFileCommand { get; }
        public DelegateCommand ExitApplicationCommand { get; }
        public DelegateCommand AboutCommand { get; }
        
        // 從服務中暴露 Undo/Redo 命令
        public DelegateCommand UndoCommand => _undoRedoService.UndoCommand;
        public DelegateCommand RedoCommand => _undoRedoService.RedoCommand;

        public ShellViewModel(IEventAggregator eventAggregator, ISubtitleParserService parserService, IUndoRedoService undoRedoService)
        {
            _eventAggregator = eventAggregator;
            _parserService = parserService;
            _undoRedoService = undoRedoService;
            
            OpenSubtitleFileCommand = new DelegateCommand(ExecuteOpenSubtitleFile);
            OpenVideoFileCommand = new DelegateCommand(ExecuteOpenVideoFile);
            SaveCommand = new DelegateCommand(ExecuteSave);
            SaveSubtitleFileCommand = new DelegateCommand(ExecuteSaveSubtitleFile);
            ExitApplicationCommand = new DelegateCommand(ExecuteExitApplication);
            AboutCommand = new DelegateCommand(ExecuteAbout);

            // 訂閱 AI 處理事件
            _eventAggregator.GetEvent<AiProcessingStartedEvent>().Subscribe(OnAiProcessingStarted);
            _eventAggregator.GetEvent<AiProcessingFinishedEvent>().Subscribe(OnAiProcessingFinished);
        }

        private void ExecuteOpenSubtitleFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "字幕檔案 (*.srt)|*.srt|All files (*.*)|*.*",
                Title = "開啟字幕檔案"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var subtitles = _parserService.Parse(dialog.FileName);
                    var project = new SubtitleProject(dialog.FileName, subtitles);
                    _eventAggregator.GetEvent<SubtitlesReadyEvent>().Publish(project);

                    // 自動尋找並載入同名影片
                    var baseFilePath = Path.ChangeExtension(dialog.FileName, null);
                    var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov" };

                    foreach (var extension in videoExtensions)
                    {
                        var videoPath = baseFilePath + extension;
                        if (File.Exists(videoPath))
                        {
                            _eventAggregator.GetEvent<LoadVideoEvent>().Publish(videoPath);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"開啟或解析字幕檔案時發生錯誤：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteOpenVideoFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "影片檔案 (*.mp4;*.mkv;*.avi;*.mov)|*.mp4;*.mkv;*.avi;*.mov|MP4 檔案 (*.mp4)|*.mp4|MKV 檔案 (*.mkv)|*.mkv|AVI 檔案 (*.avi)|*.avi|MOV 檔案 (*.mov)|*.mov|All files (*.*)|*.*",
                Title = "開啟影片檔案"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _eventAggregator.GetEvent<LoadVideoEvent>().Publish(dialog.FileName);

                    // 自動尋找並載入同名字幕檔
                    var baseFilePath = Path.ChangeExtension(dialog.FileName, null);
                    var subtitlePath = baseFilePath + ".srt";

                    if (File.Exists(subtitlePath))
                    {
                        var subtitles = _parserService.Parse(subtitlePath);
                        var project = new SubtitleProject(subtitlePath, subtitles);
                        _eventAggregator.GetEvent<SubtitlesReadyEvent>().Publish(project);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"開啟影片檔案時發生錯誤：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteSaveSubtitleFile()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "字幕檔案 (*.srt)|*.srt|All files (*.*)|*.*",
                Title = "另存新檔",
                DefaultExt = "srt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 詢問是否包含演講者
                    var result = MessageBox.Show("是否在儲存的字幕檔案中包含演講者資訊？", 
                                                "儲存選項", 
                                                MessageBoxButton.YesNo, 
                                                MessageBoxImage.Question);
                    
                    bool includeSpeaker = result == MessageBoxResult.Yes;
                    
                    var options = new SaveSubtitleOptions
                    {
                        FilePath = dialog.FileName,
                        IncludeSpeaker = includeSpeaker
                    };
                    
                    _eventAggregator.GetEvent<SaveSubtitleWithOptionsEvent>().Publish(options);
                    MessageBox.Show("字幕檔案儲存成功！", "儲存完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"儲存字幕檔案時發生錯誤：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteExitApplication()
        {
            // 如果正在進行 AI 轉換，顯示特別的確認對話框
            if (IsAiProcessing)
            {
                var result = MessageBox.Show(
                    "正在進行 AI 字幕轉換中，確定要結束嗎？\n\n結束程式將中斷 AI 轉換過程。",
                    "AI 轉換進行中",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    Application.Current.MainWindow?.Close();
            }
            }
            else
            {
                // 直接關閉主視窗，讓 Closing 事件處理確認對話框
                Application.Current.MainWindow?.Close();
            }
        }

        private void OnAiProcessingStarted()
        {
            IsAiProcessing = true;
        }

        private void OnAiProcessingFinished()
        {
            IsAiProcessing = false;
        }

        private void ExecuteSave()
        {
            // 發布儲存事件，讓 SubtitleGridViewModel 處理
            _eventAggregator.GetEvent<SaveSubtitleEvent>().Publish(string.Empty);
        }

        private void ExecuteAbout()
        {
            var aboutWindow = new Views.AboutWindow
            {
                Owner = Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        }
    }
} 