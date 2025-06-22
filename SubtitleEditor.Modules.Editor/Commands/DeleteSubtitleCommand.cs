using System;
using System.Collections.ObjectModel;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 刪除字幕的可復原命令
    /// </summary>
    public class DeleteSubtitleCommand : IUndoableCommand
    {
        private readonly ObservableCollection<SubtitleItem> _collection;
        private readonly SubtitleItem _itemToRemove;
        private readonly int _originalIndex;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="collection">字幕集合</param>
        /// <param name="itemToRemove">要刪除的字幕項目</param>
        public DeleteSubtitleCommand(ObservableCollection<SubtitleItem> collection, SubtitleItem itemToRemove)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _itemToRemove = itemToRemove ?? throw new ArgumentNullException(nameof(itemToRemove));
            
            // 記錄項目在集合中的原始索引位置
            _originalIndex = _collection.IndexOf(_itemToRemove);
            
            // 如果找不到項目，拋出例外
            if (_originalIndex < 0)
            {
                throw new ArgumentException("要刪除的項目不存在於集合中", nameof(itemToRemove));
            }
        }

        /// <summary>
        /// 執行刪除操作
        /// </summary>
        public void Execute()
        {
            _collection.Remove(_itemToRemove);
        }

        /// <summary>
        /// 復原刪除操作（將項目插回原始位置）
        /// </summary>
        public void Unexecute()
        {
            // 確保索引在有效範圍內
            if (_originalIndex >= 0 && _originalIndex <= _collection.Count)
            {
                _collection.Insert(_originalIndex, _itemToRemove);
            }
            else
            {
                // 如果原始索引超出範圍，則加到最後
                _collection.Add(_itemToRemove);
            }
        }
    }
} 