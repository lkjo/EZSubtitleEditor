using System;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 編輯文字的可復原命令
    /// </summary>
    public class EditTextCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToEdit;
        private readonly string _oldText;
        private readonly string _newText;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToEdit">要編輯的字幕項目</param>
        /// <param name="oldText">編輯前的文字</param>
        /// <param name="newText">編輯後的文字</param>
        public EditTextCommand(SubtitleItem itemToEdit, string oldText, string newText)
        {
            _itemToEdit = itemToEdit ?? throw new ArgumentNullException(nameof(itemToEdit));
            _oldText = oldText ?? string.Empty;
            _newText = newText ?? string.Empty;
        }

        /// <summary>
        /// 執行編輯操作（設定新文字）
        /// </summary>
        public void Execute()
        {
            _itemToEdit.Text = _newText;
        }

        /// <summary>
        /// 復原編輯操作（恢復舊文字）
        /// </summary>
        public void Unexecute()
        {
            _itemToEdit.Text = _oldText;
        }
    }
} 