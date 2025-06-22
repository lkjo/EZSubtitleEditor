using System.Windows.Controls;
using Prism.Events;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Views
{
    public partial class SubtitleGridView : UserControl
    {
        private readonly IEventAggregator _eventAggregator;

        public SubtitleGridView(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            InitializeComponent();
            
            // 訂閱捲動事件
            _eventAggregator.GetEvent<ScrollToItemEvent>().Subscribe(OnScrollToItem);
        }

        /// <summary>
        /// 處理捲動到指定字幕項目的事件
        /// </summary>
        /// <param name="item">要捲動到的字幕項目</param>
        private void OnScrollToItem(SubtitleItem item)
        {
            if (item != null)
            {
                SubtitlesDataGrid.ScrollIntoView(item);
            }
        }
    }
}
