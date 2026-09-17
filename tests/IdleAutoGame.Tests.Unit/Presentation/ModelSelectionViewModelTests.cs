using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Tests.Unit.Fakes;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class ModelSelectionViewModelTests
{
    private readonly IModelCatalog _catalog;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IConfigurationService _configService;
    private readonly FakeActiveContextService _activeContext;
    private readonly LocalLlamaProvider _localProvider;

    public ModelSelectionViewModelTests()
    {
        _catalog = new JsonModelCatalog();
        _modelManager = Substitute.For<IModelManager>();
        _hardwareDetector = Substitute.For<IHardwareDetector>();
        _configService = Substitute.For<IConfigurationService>();
        _activeContext = new FakeActiveContextService();
        _localProvider = new LocalLlamaProvider();

        var settings = new AppSettings();
        _configService.Current.Returns(settings);
        _configService.UpdateSettingsAsync(Arg.Any<AppSettings>()).Returns(new IdleAutoGame.Application.Validation.ValidationResult());
    }

    [Fact]
    public async Task LoadModelsAsync_PopulatesThreeRecommendedLocalModelsForHardwareTier()
    {
        // 16 GB hardware -> Tier16Gb
        _hardwareDetector.DetectAsync().Returns(new HardwareInfo
        {
            TotalRamMb = 16384,
            AvailableRamMb = 12000,
            CpuCores = 8
        });

        var vm = new ModelSelectionViewModel(_catalog, _modelManager, _hardwareDetector, _configService, _activeContext, _localProvider);

        await vm.LoadModelsAsync();

        vm.DetectedRamTierText.Should().Contain("16 GB");
        vm.RecommendedLocalModels.Should().HaveCount(3);
        vm.RecommendedLocalModels.Should().OnlyContain(m => m.Model.RamTier == IdleAutoGame.Core.Enums.RamTier.Tier16Gb);
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenLocalModelNotInstalled_BlocksActivation()
    {
        _hardwareDetector.DetectAsync().Returns(new HardwareInfo { TotalRamMb = 16384, AvailableRamMb = 12000 });
        _modelManager.IsModelInstalledAsync(Arg.Any<string>()).Returns(false);

        var vm = new ModelSelectionViewModel(_catalog, _modelManager, _hardwareDetector, _configService, _activeContext, _localProvider);
        await vm.LoadModelsAsync();

        vm.IsLocalMode = true;
        vm.SelectedLocalModel = vm.RecommendedLocalModels.First();

        await vm.SaveSelectionAsync();

        vm.StatusMessage.Should().Contain("is not installed");
        await _configService.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>());
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenLocalModelIncompatible_BlocksActivation()
    {
        // 4 GB RAM machine
        _hardwareDetector.DetectAsync().Returns(new HardwareInfo { TotalRamMb = 4096, AvailableRamMb = 2000 });
        _modelManager.IsModelInstalledAsync(Arg.Any<string>()).Returns(true);

        var vm = new ModelSelectionViewModel(_catalog, _modelManager, _hardwareDetector, _configService, _activeContext, _localProvider);
        await vm.LoadModelsAsync();

        vm.IsLocalMode = true;
        var incompatible = vm.RecommendedLocalModels.FirstOrDefault(m => !m.IsCompatible);

        if (incompatible != null)
        {
            vm.SelectedLocalModel = incompatible;
            await vm.SaveSelectionAsync();

            vm.StatusMessage.Should().Contain("Selection blocked: Incompatible");
            await _configService.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>());
        }
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenRemoteMode_UpdatesRemoteSettings()
    {
        _hardwareDetector.DetectAsync().Returns(new HardwareInfo { TotalRamMb = 16384 });

        var vm = new ModelSelectionViewModel(_catalog, _modelManager, _hardwareDetector, _configService, _activeContext, _localProvider);
        await vm.LoadModelsAsync();

        vm.IsLocalMode = false;
        vm.Endpoint = "https://api.openai.com/v1";
        vm.ApiKey = "sk-secret-test";
        vm.SelectedRemoteItem = vm.AvailableRemoteModels.FirstOrDefault();

        await vm.SaveSelectionAsync();

        vm.StatusMessage.Should().Contain("activated");
        await _configService.Received(1).UpdateSettingsAsync(Arg.Is<AppSettings>(s =>
            s.Llm.Endpoint == "https://api.openai.com/v1" &&
            s.Llm.ApiKey == "sk-secret-test"));
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenHardwareNotSupported_FallsBackToLlamaCppMode()
    {
        if (LocalLlamaProvider.IsHardwareSupported(out _))
        {
            return;
        }

        var tempFile = Path.GetTempFileName();
        try
        {
            _hardwareDetector.DetectAsync().Returns(new HardwareInfo { TotalRamMb = 16384, AvailableRamMb = 12000 });
            _modelManager.IsModelInstalledAsync(Arg.Any<string>()).Returns(true);
            _modelManager.GetModelFilePath(Arg.Any<string>()).Returns(tempFile);

            var vm = new ModelSelectionViewModel(_catalog, _modelManager, _hardwareDetector, _configService, _activeContext, _localProvider);
            await vm.LoadModelsAsync();

            vm.IsLocalMode = true;
            vm.SelectedLocalModel = vm.RecommendedLocalModels.First(m => m.IsCompatible);

            await vm.SaveSelectionAsync();

            vm.StatusMessage.Should().Contain("llama.cpp");
            await _configService.Received().UpdateSettingsAsync(Arg.Is<AppSettings>(s => s.Llm.Provider == "llama.cpp"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

