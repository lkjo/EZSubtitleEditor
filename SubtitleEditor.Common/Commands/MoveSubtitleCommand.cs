using System;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Commands
{
    /// <summary>
    /// 移動字幕時間的可復原命令
    /// </summary>
    public class MoveSubtitleCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToMove;
        private readonly string _originalStartTime;
        private readonly string _originalEndTime;
        private readonly string _newStartTime;
        private readonly string _newEndTime;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToMove">要移動的字幕項目</param>
        /// <param name="originalStartTime">移動前的原始開始時間</param>
        /// <param name="originalEndTime">移動前的原始結束時間</param>
        public MoveSubtitleCommand(SubtitleItem itemToMove, string originalStartTime, string originalEndTime)
        {
            _itemToMove = itemToMove ?? throw new ArgumentNullException(nameof(itemToMove));
            _originalStartTime = originalStartTime ?? throw new ArgumentNullException(nameof(originalStartTime));
            _originalEndTime = originalEndTime ?? throw new ArgumentNullException(nameof(originalEndTime));
            
            // 記錄拖動後的新時間（建構時字幕已經被拖動到新位置）
            _newStartTime = itemToMove.StartTime;
            _newEndTime = itemToMove.EndTime;
            
            // System.Diagnostics.Debug.WriteLine($"MoveSubtitleCommand 建立: 原始[{_originalStartTime}-{_originalEndTime}] -> 新的[{_newStartTime}-{_newEndTime}]");
        }

        /// <summary>
        /// 執行移動操作（恢復到拖動後的新時間）
        /// </summary>
        public void Execute()
        {
            _itemToMove.StartTime = _newStartTime;
            _itemToMove.EndTime = _newEndTime;
            // System.Diagnostics.Debug.WriteLine($"MoveSubtitleCommand Execute: 恢復到新時間 [{_newStartTime}-{_newEndTime}]");
        }

        /// <summary>
        /// 復原移動操作（將時間恢復為原始值）
        /// </summary>
        public void Unexecute()
        {
            _itemToMove.StartTime = _originalStartTime;
            _itemToMove.EndTime = _originalEndTime;
            // System.Diagnostics.Debug.WriteLine($"MoveSubtitleCommand Unexecute: 恢復到原始時間 [{_originalStartTime}-{_originalEndTime}]");
        }
    }
} 