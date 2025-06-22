using System;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Modules.Editor.Commands
{
    /// <summary>
    /// 編輯演講者的可復原命令
    /// </summary>
    public class EditSpeakerCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToEdit;
        private readonly string _oldSpeaker;
        private readonly string _newSpeaker;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToEdit">要編輯的字幕項目</param>
        /// <param name="oldSpeaker">編輯前的演講者</param>
        /// <param name="newSpeaker">編輯後的演講者</param>
        public EditSpeakerCommand(SubtitleItem itemToEdit, string oldSpeaker, string newSpeaker)
        {
            _itemToEdit = itemToEdit ?? throw new ArgumentNullException(nameof(itemToEdit));
            _oldSpeaker = oldSpeaker ?? string.Empty;
            _newSpeaker = newSpeaker ?? string.Empty;
        }

        /// <summary>
        /// 執行編輯操作（設定新演講者）
        /// </summary>
        public void Execute()
        {
            _itemToEdit.Speaker = _newSpeaker;
        }

        /// <summary>
        /// 復原編輯操作（恢復舊演講者）
        /// </summary>
        public void Unexecute()
        {
            _itemToEdit.Speaker = _oldSpeaker;
        }
    }
} 