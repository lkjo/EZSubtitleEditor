using System;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 編輯結束時間的可復原命令
    /// </summary>
    public class EditEndTimeCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToEdit;
        private readonly string _oldEndTime;
        private readonly string _newEndTime;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToEdit">要編輯的字幕項目</param>
        /// <param name="oldEndTime">編輯前的結束時間</param>
        /// <param name="newEndTime">編輯後的結束時間</param>
        public EditEndTimeCommand(SubtitleItem itemToEdit, string oldEndTime, string newEndTime)
        {
            _itemToEdit = itemToEdit ?? throw new ArgumentNullException(nameof(itemToEdit));
            _oldEndTime = oldEndTime ?? string.Empty;
            _newEndTime = newEndTime ?? string.Empty;
        }

        /// <summary>
        /// 執行編輯操作（設定新結束時間）
        /// </summary>
        public void Execute()
        {
            _itemToEdit.EndTime = _newEndTime;
        }

        /// <summary>
        /// 復原編輯操作（恢復舊結束時間）
        /// </summary>
        public void Unexecute()
        {
            _itemToEdit.EndTime = _oldEndTime;
        }
    }
} 