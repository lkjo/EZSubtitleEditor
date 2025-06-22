using Prism.Commands;
using SubtitleEditor.Common.Commands;

namespace SubtitleEditor.Common.Services
{
    /// <summary>
    /// Undo/Redo 服務介面
    /// </summary>
    public interface IUndoRedoService
    {
        /// <summary>
        /// 執行一個新命令
        /// </summary>
        /// <param name="command">要執行的命令</param>
        void Do(IUndoableCommand command);

        /// <summary>
        /// 撤銷上一個命令
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做上一個被撤銷的命令
        /// </summary>
        void Redo();

        /// <summary>
        /// 取得是否可以撤銷
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 取得是否可以重做
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// 復原命令
        /// </summary>
        DelegateCommand UndoCommand { get; }

        /// <summary>
        /// 取消復原命令
        /// </summary>
        DelegateCommand RedoCommand { get; }
    }
} 