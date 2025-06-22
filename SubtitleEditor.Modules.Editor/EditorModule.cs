using Prism.Ioc;
using Prism.Modularity;
using SubtitleEditor.Modules.Editor.Views;

namespace SubtitleEditor.Modules.Editor
{
    public class EditorModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 註冊 SubtitleGridView 用於導航
            containerRegistry.RegisterForNavigation<SubtitleGridView>();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 取得 RegionManager 並請求將 SubtitleGridView 顯示在 SubtitleEditorRegion 中
            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RequestNavigate("SubtitleEditorRegion", nameof(SubtitleGridView));
        }
    }
} 