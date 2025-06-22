using SubtitleEditor.Modules.Player.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace SubtitleEditor.Modules.Player
{
    public class PlayerModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RequestNavigate("VideoPlayerRegion", nameof(VideoPlayerView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<VideoPlayerView>();
        }
    }
}