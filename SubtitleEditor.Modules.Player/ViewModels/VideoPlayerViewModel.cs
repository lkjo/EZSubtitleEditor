using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Windows;
using LibVLCSharp.Shared;
using SubtitleEditor.Common.Events;

namespace SubtitleEditor.Modules.Player.ViewModels
{
    public class VideoPlayerViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;

        private string _message = string.Empty;
        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        private float _position;
        private bool _isUpdatingPosition = false;
        public float Position
        {
            get { return _position; }
            set 
            {
                if (SetProperty(ref _position, value) && !_isUpdatingPosition)
                {
                    if (MediaPlayer != null)
                        MediaPlayer.Position = value;
                }
            }
        }

        private long _time;
        public long Time
        {
            get { return _time; }
            set { SetProperty(ref _time, value); }
        }

        private long _length;
        public long Length
        {
            get { return _length; }
            set { SetProperty(ref _length, value); }
        }

        private string _playbackRate = "1.0";
        public string PlaybackRate
        {
            get { return _playbackRate; }
            set 
            { 
                if (SetProperty(ref _playbackRate, value))
                {
                    if (MediaPlayer != null && float.TryParse(value, out float rate))
                        MediaPlayer.SetRate(rate);
                }
            }
        }

        private int _volume = 100;
        public int Volume
        {
            get { return _volume; }
            set 
            { 
                if (SetProperty(ref _volume, value))
                {
                    if (MediaPlayer != null)
                        MediaPlayer.Volume = value;
                }
            }
        }

        public LibVLC LibVLC { get; }
        public MediaPlayer MediaPlayer { get; }
        public DelegateCommand PlayCommand { get; }
        public DelegateCommand PauseCommand { get; }

        public VideoPlayerViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            Message = "影片播放器模組";
            
            LibVLC = new LibVLC();
            MediaPlayer = new MediaPlayer(LibVLC);

            // 初始化命令
            PlayCommand = new DelegateCommand(ExecutePlay);
            PauseCommand = new DelegateCommand(ExecutePause);

            // 訂閱 MediaPlayer 事件
            MediaPlayer.PositionChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => 
            {
                _isUpdatingPosition = true;
                Position = e.Position;
                _isUpdatingPosition = false;
            });
            MediaPlayer.TimeChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => 
            {
                Time = e.Time;
                // 發布時間變化事件
                var timeSpan = TimeSpan.FromMilliseconds(e.Time);
                _eventAggregator.GetEvent<PlayerTimeChangedEvent>().Publish(timeSpan);
            });
            MediaPlayer.LengthChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => 
            {
                Length = e.Length;
                // 發布影片長度變化事件
                _eventAggregator.GetEvent<VideoLengthChangedEvent>().Publish(e.Length);
            });

            // 訂閱載入影片事件
            _eventAggregator.GetEvent<LoadVideoEvent>().Subscribe(LoadAndPlay);
            
            // 訂閱影片跳轉事件
            _eventAggregator.GetEvent<SeekVideoEvent>().Subscribe(OnSeekVideo);
        }

        public void LoadAndPlay(string filePath)
        {
            MediaPlayer.Play(new Media(LibVLC, filePath, FromType.FromPath));
            
            // 發布影片載入事件
            _eventAggregator.GetEvent<VideoLoadedEvent>().Publish(filePath);
        }

        private void ExecutePlay()
        {
            MediaPlayer.Play();
        }

        private void ExecutePause()
        {
            MediaPlayer.Pause();
        }

        private void OnSeekVideo(TimeSpan time)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Time = (long)time.TotalMilliseconds;
            }
        }


    }
}
