using System.Collections.ObjectModel;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 新增字幕的可復原命令
    /// </summary>
    public class AddSubtitleCommand : IUndoableCommand
    {
        private readonly ObservableCollection<SubtitleItem> _collection;
        private readonly SubtitleItem _itemToAdd;
        private readonly int _insertIndex;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="collection">字幕集合</param>
        /// <param name="itemToAdd">要新增的字幕項目</param>
        /// <param name="insertIndex">插入位置索引，如果為 -1 則加到最後</param>
        public AddSubtitleCommand(ObservableCollection<SubtitleItem> collection, SubtitleItem itemToAdd, int insertIndex = -1)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _itemToAdd = itemToAdd ?? throw new ArgumentNullException(nameof(itemToAdd));
            _insertIndex = insertIndex;
        }

        /// <summary>
        /// 執行新增操作
        /// </summary>
        public void Execute()
        {
            if (_insertIndex >= 0 && _insertIndex < _collection.Count)
            {
                _collection.Insert(_insertIndex, _itemToAdd);
            }
            else
            {
                _collection.Add(_itemToAdd);
            }
        }

        /// <summary>
        /// 復原新增操作（移除項目）
        /// </summary>
        public void Unexecute()
        {
            _collection.Remove(_itemToAdd);
        }
    }
} 