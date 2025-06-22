using Prism.Ioc;
using Prism.Modularity;
using SubtitleEditor.Modules.Timeline.Views;
using SubtitleEditor.Modules.Timeline.ViewModels;

namespace SubtitleEditor.Modules.Timeline
{
    /// <summary>
    /// Timeline 模組
    /// </summary>
    public class TimelineModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RegisterViewWithRegion("TimelineRegion", typeof(TimelineView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 註冊 Views 和 ViewModels
        }
    }
} 