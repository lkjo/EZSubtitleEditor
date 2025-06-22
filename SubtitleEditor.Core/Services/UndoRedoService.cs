using System.Collections.Generic;
using Prism.Commands;
using Prism.Events;
using SubtitleEditor.Common.Commands;
using SubtitleEditor.Common.Events;
using SubtitleEditor.Common.Models;
using SubtitleEditor.Common.Services;

namespace SubtitleEditor.Core.Services
{
    /// <summary>
    /// Undo/Redo 服務實作
    /// </summary>
    public class UndoRedoService : IUndoRedoService
    {
        private readonly Stack<IUndoableCommand> _undoStack;
        private readonly Stack<IUndoableCommand> _redoStack;
        private readonly IEventAggregator _eventAggregator;

        public DelegateCommand UndoCommand { get; }
        public DelegateCommand RedoCommand { get; }

        public UndoRedoService(IEventAggregator eventAggregator)
        {
            _undoStack = new Stack<IUndoableCommand>();
            _redoStack = new Stack<IUndoableCommand>();
            _eventAggregator = eventAggregator;
            
            // 初始化命令
            UndoCommand = new DelegateCommand(Undo, () => CanUndo);
            RedoCommand = new DelegateCommand(Redo, () => CanRedo);
        }

        /// <summary>
        /// 取得是否可以撤銷
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 取得是否可以重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 執行一個新命令
        /// </summary>
        /// <param name="command">要執行的命令</param>
        public void Do(IUndoableCommand command)
        {
            if (command == null)
                return;

            // 執行命令
            command.Execute();

            // 發布字幕更新事件
            PublishSubtitleUpdatedEventIfNeeded(command);

            // 將命令推入撤銷堆疊
            _undoStack.Push(command);

            // 清空重做堆疊（因為執行了新命令，之前的重做歷史就無效了）
            _redoStack.Clear();

            // 更新命令狀態
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 撤銷上一個命令
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            // 從撤銷堆疊彈出命令
            var command = _undoStack.Pop();

            // 執行撤銷操作
            command.Unexecute();

            // 發布字幕更新事件
            PublishSubtitleUpdatedEventIfNeeded(command);

            // 將命令推入重做堆疊
            _redoStack.Push(command);

            // 更新命令狀態
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 重做上一個被撤銷的命令
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            // 從重做堆疊彈出命令
            var command = _redoStack.Pop();

            // 重新執行命令
            command.Execute();

            // 發布字幕更新事件
            PublishSubtitleUpdatedEventIfNeeded(command);

            // 將命令推入撤銷堆疊
            _undoStack.Push(command);

            // 更新命令狀態
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 如果命令涉及字幕編輯，發布字幕更新事件
        /// </summary>
        /// <param name="command">執行的命令</param>
        private void PublishSubtitleUpdatedEventIfNeeded(IUndoableCommand command)
        {
            try
            {
                // 使用反射來檢查命令類型和獲取字幕項目
                var commandType = command.GetType();
                var commandName = commandType.Name;

                SubtitleItem? subtitleItem = null;

                // 檢查是否為字幕編輯相關的命令（以 Edit 開頭）
                if (commandName.StartsWith("Edit") && commandName.EndsWith("Command"))
                {
                    // 嘗試獲取 _itemToEdit 欄位
                    var itemToEditField = commandType.GetField("_itemToEdit", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    subtitleItem = itemToEditField?.GetValue(command) as SubtitleItem;
                }
                // 檢查是否為移動字幕命令
                else if (commandName == "MoveSubtitleCommand")
                {
                    // 嘗試獲取 _itemToMove 欄位
                    var itemToMoveField = commandType.GetField("_itemToMove", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    subtitleItem = itemToMoveField?.GetValue(command) as SubtitleItem;
                }
                // 檢查是否為調整尺寸字幕命令
                else if (commandName == "ResizeSubtitleCommand")
                {
                    // 嘗試獲取 _itemToResize 欄位
                    var itemToResizeField = commandType.GetField("_itemToResize", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    subtitleItem = itemToResizeField?.GetValue(command) as SubtitleItem;
                }

                // 如果找到字幕項目，發布更新事件
                if (subtitleItem != null)
                {
                    _eventAggregator.GetEvent<SubtitleUpdatedEvent>().Publish(subtitleItem);
                    // System.Diagnostics.Debug.WriteLine($"UndoRedoService 發布字幕更新事件: {subtitleItem.Text} [{subtitleItem.StartTime} - {subtitleItem.EndTime}]");
                }
            }
            catch (System.Exception ex)
            {
                // 發布事件失敗不應該影響命令執行
                // System.Diagnostics.Debug.WriteLine($"發布字幕更新事件失敗: {ex.Message}");
            }
        }
    }
} 