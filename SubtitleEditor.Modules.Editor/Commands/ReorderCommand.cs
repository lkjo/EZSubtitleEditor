using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 重新排序的可復原命令
    /// </summary>
    public class ReorderCommand : IUndoableCommand
    {
        private readonly ObservableCollection<SubtitleItem> _collection;
        private readonly int _oldIndex;
        private readonly int _newIndex;
        private readonly Dictionary<SubtitleItem, int> _originalIndexes;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="collection">字幕集合</param>
        /// <param name="oldIndex">原始索引位置</param>
        /// <param name="newIndex">新的索引位置</param>
        public ReorderCommand(ObservableCollection<SubtitleItem> collection, int oldIndex, int newIndex)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _oldIndex = oldIndex;
            _newIndex = newIndex;

            // 驗證索引範圍
            if (_oldIndex < 0 || _oldIndex >= _collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(oldIndex), "原始索引超出範圍");
            }
            if (_newIndex < 0 || _newIndex >= _collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex), "新索引超出範圍");
            }

            // 保存所有項目的原始編號
            _originalIndexes = new Dictionary<SubtitleItem, int>();
            foreach (var item in _collection)
            {
                _originalIndexes[item] = item.Index;
            }
        }

        /// <summary>
        /// 執行重新排序操作
        /// </summary>
        public void Execute()
        {
            _collection.Move(_oldIndex, _newIndex);
            UpdateIndexes();
        }

        /// <summary>
        /// 復原重新排序操作
        /// </summary>
        public void Unexecute()
        {
            _collection.Move(_newIndex, _oldIndex);
            RestoreOriginalIndexes();
        }

        /// <summary>
        /// 更新所有項目的編號為連續數字
        /// </summary>
        private void UpdateIndexes()
        {
            for (int i = 0; i < _collection.Count; i++)
            {
                _collection[i].Index = i + 1;
            }
        }

        /// <summary>
        /// 恢復所有項目的原始編號
        /// </summary>
        private void RestoreOriginalIndexes()
        {
            foreach (var item in _collection)
            {
                if (_originalIndexes.TryGetValue(item, out int originalIndex))
                {
                    item.Index = originalIndex;
                }
            }
        }
    }
} 