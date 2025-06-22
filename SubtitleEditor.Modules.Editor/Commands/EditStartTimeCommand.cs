using System;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 編輯開始時間的可復原命令
    /// </summary>
    public class EditStartTimeCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToEdit;
        private readonly string _oldStartTime;
        private readonly string _newStartTime;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToEdit">要編輯的字幕項目</param>
        /// <param name="oldStartTime">編輯前的開始時間</param>
        /// <param name="newStartTime">編輯後的開始時間</param>
        public EditStartTimeCommand(SubtitleItem itemToEdit, string oldStartTime, string newStartTime)
        {
            _itemToEdit = itemToEdit ?? throw new ArgumentNullException(nameof(itemToEdit));
            _oldStartTime = oldStartTime ?? string.Empty;
            _newStartTime = newStartTime ?? string.Empty;
        }

        /// <summary>
        /// 執行編輯操作（設定新開始時間）
        /// </summary>
        public void Execute()
        {
            _itemToEdit.StartTime = _newStartTime;
        }

        /// <summary>
        /// 復原編輯操作（恢復舊開始時間）
        /// </summary>
        public void Unexecute()
        {
            _itemToEdit.StartTime = _oldStartTime;
        }
    }
} 