using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using SubtitleEditor.Common.Services;   // <== 為了 ISubtitleParserService、ISubtitleWriterService、IUndoRedoService
using SubtitleEditor.Core.Services;     // <== 為了 SrtParserService、UndoRedoService
using SubtitleEditor.Modules.Editor;    // <== 為了 EditorModule
using SubtitleEditor.Modules.Player;    // <== 為了 PlayerModule
using SubtitleEditor.Modules.Timeline;  // <== 為了 TimelineModule
using SubtitleEditor.UI.Views;          // <== 為了 ShellView
using System.Windows;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using Microsoft.Extensions.DependencyInjection;
using SubtitleEditor.Common.Enums;

namespace SubtitleEditor.UI
{
    // 修正點 1: 必須繼承 PrismApplication
    public partial class App : PrismApplication
    {
        public App()
        {
            LibVLCSharp.Shared.Core.Initialize();
        }
        protected override Window CreateShell()
        {
            // 修正點 2: 我們的主視窗是 ShellView，不是 MainWindow
            return Container.Resolve<ShellView>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 註冊 Configuration 系統
            var configuration = BuildConfiguration();
            containerRegistry.RegisterInstance<IConfiguration>(configuration);

            // 註冊我們的服務
            containerRegistry.RegisterSingleton<ISubtitleParserService, SrtParserService>();
            containerRegistry.RegisterSingleton<ISubtitleWriterService, SrtWriterService>();
            containerRegistry.RegisterSingleton<IUndoRedoService, UndoRedoService>();
            
            // 註冊 AI 服務工廠（替換直接註冊 IAiTranscriptionService）
            containerRegistry.RegisterSingleton<IAiServiceFactory, AiServiceFactory>();
            
            // 註冊音訊處理服務
            containerRegistry.RegisterSingleton<IAudioProcessingService, AudioProcessingService>();
            
            // 註冊對話方塊
            containerRegistry.RegisterDialog<Views.AiSettingsView, ViewModels.AiSettingsViewModel>(nameof(Views.AiSettingsView));
        }

        /// <summary>
        /// 建立 Configuration 實例
        /// </summary>
        /// <returns>IConfiguration 實例</returns>
        private IConfiguration BuildConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            return configurationBuilder.Build();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 註冊我們的模組
            moduleCatalog.AddModule<EditorModule>();
            moduleCatalog.AddModule<PlayerModule>();
            moduleCatalog.AddModule<TimelineModule>();
        }
    }
}