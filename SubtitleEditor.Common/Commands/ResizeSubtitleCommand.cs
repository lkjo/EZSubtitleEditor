using System;
using SubtitleEditor.Common.Models;

namespace SubtitleEditor.Common.Commands
{
    /// <summary>
    /// 調整字幕時間長度的可復原命令
    /// </summary>
    public class ResizeSubtitleCommand : IUndoableCommand
    {
        private readonly SubtitleItem _itemToResize;
        private readonly TimeSpan _oldStartTime;
        private readonly TimeSpan _oldEndTime;
        private readonly TimeSpan _newStartTime;
        private readonly TimeSpan _newEndTime;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="itemToResize">要調整尺寸的字幕項目</param>
        /// <param name="oldStartTime">原始開始時間</param>
        /// <param name="oldEndTime">原始結束時間</param>
        /// <param name="newStartTime">新的開始時間</param>
        /// <param name="newEndTime">新的結束時間</param>
        public ResizeSubtitleCommand(SubtitleItem itemToResize, TimeSpan oldStartTime, TimeSpan oldEndTime, TimeSpan newStartTime, TimeSpan newEndTime)
        {
            _itemToResize = itemToResize ?? throw new ArgumentNullException(nameof(itemToResize));
            _oldStartTime = oldStartTime;
            _oldEndTime = oldEndTime;
            _newStartTime = newStartTime;
            _newEndTime = newEndTime;

            // System.Diagnostics.Debug.WriteLine($"ResizeSubtitleCommand 建立: 原始[{FormatTimeSpan(_oldStartTime)}-{FormatTimeSpan(_oldEndTime)}] -> 新的[{FormatTimeSpan(_newStartTime)}-{FormatTimeSpan(_newEndTime)}]");
        }

        /// <summary>
        /// 執行尺寸調整操作（套用新的時間）
        /// </summary>
        public void Execute()
        {
            _itemToResize.StartTime = FormatTimeSpan(_newStartTime);
            _itemToResize.EndTime = FormatTimeSpan(_newEndTime);
            // System.Diagnostics.Debug.WriteLine($"ResizeSubtitleCommand Execute: 套用新時間 [{FormatTimeSpan(_newStartTime)}-{FormatTimeSpan(_newEndTime)}]");
        }

        /// <summary>
        /// 復原尺寸調整操作（恢復為原始時間）
        /// </summary>
        public void Unexecute()
        {
            _itemToResize.StartTime = FormatTimeSpan(_oldStartTime);
            _itemToResize.EndTime = FormatTimeSpan(_oldEndTime);
            // System.Diagnostics.Debug.WriteLine($"ResizeSubtitleCommand Unexecute: 恢復到原始時間 [{FormatTimeSpan(_oldStartTime)}-{FormatTimeSpan(_oldEndTime)}]");
        }

        /// <summary>
        /// 將 TimeSpan 格式化為字幕時間格式
        /// </summary>
        /// <param name="timeSpan">時間跨度</param>
        /// <returns>格式化的時間字串</returns>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // 使用十分之一秒精度，與SRT格式一致（如 00:00:00,0）
            var tenthsOfSecond = (timeSpan.Milliseconds + 50) / 100; // 四捨五入到十分之一秒
            if (tenthsOfSecond >= 10)
            {
                tenthsOfSecond = 9; // 最大為 9（因為是十分之一秒）
            }
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{tenthsOfSecond}";
        }
    }
} 