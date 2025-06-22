using Prism.Mvvm;

namespace SubtitleEditor.Modules.Timeline.ViewModels
{
    /// <summary>
    /// 時間刻度的 ViewModel
    /// </summary>
    public class TimeMarkerViewModel : BindableBase
    {
        private double _left;
        /// <summary>
        /// 刻度在畫布上的位置
        /// </summary>
        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        private string _displayTime = string.Empty;
        /// <summary>
        /// 要顯示的時間文字（例如 "00:15"）
        /// </summary>
        public string DisplayTime
        {
            get => _displayTime;
            set => SetProperty(ref _displayTime, value);
        }
    }
} 