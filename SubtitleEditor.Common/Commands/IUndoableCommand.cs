namespace SubtitleEditor.Common.Commands
{
    /// <summary>
    /// 可撤銷命令介面
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// 執行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤銷命令
        /// </summary>
        void Unexecute();
    }
} 