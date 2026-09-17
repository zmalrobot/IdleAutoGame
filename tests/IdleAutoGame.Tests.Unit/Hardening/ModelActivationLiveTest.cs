using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Infrastructure.Persistence;
using IdleAutoGame.Presentation.ViewModels;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Hardening;

public class ModelActivationLiveTest
{
    [Fact]
    public async Task LiveModelActivation_DoesNotCrashHostProcess()
    {
        var tempSettingsDir = Path.Combine(Path.GetTempPath(), "IdleAutoGame_Test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSettingsDir);
        var settingsPath = Path.Combine(tempSettingsDir, "settings.json");

        try
        {
            var repo = new JsonSettingsRepository(settingsPath);
            var configService = new ConfigurationService(repo);
            await configService.InitializeAsync();

            var detector = new LinuxHardwareDetector();
            var hw = await detector.DetectAsync();

            using var httpClient = new HttpClient();
            var catalog = new JsonModelCatalog();
            var modelManager = new LocalModelManager(httpClient, catalog, configService);
            using var localProvider = new LocalLlamaProvider();

            var vm = new ModelSelectionViewModel(catalog, modelManager, detector, configService, localProvider);
            await vm.LoadModelsAsync();

            // Check if qwen2.5-vl-7b-q6 is on disk
            var existingModelFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/IdleAutoGame/models/qwen2.5-vl-7b-q6.gguf");
            if (File.Exists(existingModelFile))
            {
                var qwenModel = vm.RecommendedLocalModels.FirstOrDefault(m => m.Model.Id == "qwen2.5-vl-7b-q6") 
                                ?? vm.RecommendedLocalModels.FirstOrDefault();

                if (qwenModel != null)
                {
                    vm.IsLocalMode = true;
                    vm.SelectedLocalModel = qwenModel;

                    // This was previously triggering SIGILL / exit code 132
                    await vm.SaveSelectionAsync();

                    // Must NOT crash, and must update settings cleanly
                    configService.Current.Llm.SelectedModelId.Should().Be(qwenModel.Model.Id);
                    vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
                }
            }

            // Also test SettingsViewModel
            var settingsVm = new SettingsViewModel(configService, modelManager, detector, localProvider);
            await settingsVm.RefreshLocalModelsAsync();

            var installedModel = settingsVm.InstalledLocalModels.FirstOrDefault();
            if (installedModel != null)
            {
                await settingsVm.SelectActiveModelAsync(installedModel);
                settingsVm.StatusMessage.Should().NotBeNullOrWhiteSpace();
            }
        }
        finally
        {
            if (Directory.Exists(tempSettingsDir))
            {
                Directory.Delete(tempSettingsDir, true);
            }
        }
    }
}

