using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;

namespace IdleAutoGame.Presentation.ViewModels;

public sealed partial class LocalModelDisplayItem : ObservableObject
{
    public LocalModel Model { get; }

    [ObservableProperty]
    private bool _isCompatible;

    [ObservableProperty]
    private string _compatibilityReason;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadPercentage;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    [ObservableProperty]
    private string _downloadEtaText = string.Empty;

    [ObservableProperty]
    private ModelStatus _status;

    public LocalModelDisplayItem(LocalModel model, bool isCompatible, string compatibilityReason, bool isInstalled)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _isCompatible = isCompatible;
        _compatibilityReason = compatibilityReason;
        _isInstalled = isInstalled;
        _status = model.Status;
    }
}

public sealed record ModelDisplayItem(
    ModelProfile Profile,
    bool IsCompatible,
    string CompatibilityReason);

public partial class ModelSelectionViewModel : ViewModelBase
{
    private readonly IModelCatalog _catalog;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IConfigurationService _configService;
    private readonly LocalLlamaProvider _localProvider;

    [ObservableProperty]
    private bool _isLocalMode = true;

    [ObservableProperty]
    private string _detectedRamText = "Detecting...";

    [ObservableProperty]
    private string _detectedRamTierText = "Tier: Balanced (16 GB)";

    [ObservableProperty]
    private ObservableCollection<LocalModelDisplayItem> _recommendedLocalModels = new();

    [ObservableProperty]
    private LocalModelDisplayItem? _selectedLocalModel;

    [ObservableProperty]
    private ObservableCollection<ModelDisplayItem> _availableRemoteModels = new();

    [ObservableProperty]
    private ModelDisplayItem? _selectedRemoteItem;

    [ObservableProperty]
    private string _endpoint = "http://localhost:8080";

    [ObservableProperty]
    private string? _apiKey;

    [ObservableProperty]
    private string _statusMessage = "Select an AI model profile.";

    [ObservableProperty]
    private bool _isBusy;

    public ModelSelectionViewModel(
        IModelCatalog catalog,
        IModelManager modelManager,
        IHardwareDetector hardwareDetector,
        IConfigurationService configService,
        LocalLlamaProvider localProvider)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _localProvider = localProvider ?? throw new ArgumentNullException(nameof(localProvider));

        _modelManager.DownloadProgressChanged += OnDownloadProgressChanged;
        _modelManager.ModelStatusChanged += OnModelStatusChanged;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadModelsAsync()
    {
        IsBusy = true;
        StatusMessage = "Evaluating hardware specifications and loading model catalog...";

        try
        {
            var hardware = await _hardwareDetector.DetectAsync();
            DetectedRamText = $"{hardware.TotalRamMb / 1024.0:F1} GB RAM (Available: {hardware.AvailableRamMb / 1024.0:F1} GB)";

            var tier = _catalog.DetermineRamTier(hardware);
            DetectedRamTierText = tier switch
            {
                RamTier.Tier8Gb => "RAM Tier: Entry (≤ 8 GB)",
                RamTier.Tier16Gb => "RAM Tier: Balanced (8–16 GB)",
                _ => "RAM Tier: Performance (16–32+ GB)"
            };

            // 1. Load 3 Recommended Local Models for Tier
            var recommended = _catalog.GetRecommendedModelsForTier(tier);
            RecommendedLocalModels.Clear();

            foreach (var lm in recommended)
            {
                var compatible = lm.IsCompatibleWith(hardware, out var reason);
                var installed = await _modelManager.IsModelInstalledAsync(lm.Id);
                var item = new LocalModelDisplayItem(lm, compatible, reason, installed);
                RecommendedLocalModels.Add(item);
            }

            // 2. Load Remote Models
            var allProfiles = _catalog.GetAllModels();
            AvailableRemoteModels.Clear();
            foreach (var p in allProfiles.Where(p => !p.IsLocal || p.Provider != "LLamaSharp"))
            {
                var compatible = p.IsCompatibleWith(hardware, out var reason);
                AvailableRemoteModels.Add(new ModelDisplayItem(p, compatible, reason));
            }

            var currentSettings = _configService.Current.Llm;
            Endpoint = currentSettings.Endpoint;
            ApiKey = currentSettings.ApiKey;

            IsLocalMode = currentSettings.Provider.Equals("LLamaSharp", StringComparison.OrdinalIgnoreCase) ||
                          currentSettings.Provider.Equals("local", StringComparison.OrdinalIgnoreCase);

            var activeId = currentSettings.SelectedModelId;
            SelectedLocalModel = RecommendedLocalModels.FirstOrDefault(i => i.Model.Id == activeId)
                                 ?? RecommendedLocalModels.FirstOrDefault(i => i.IsCompatible && i.IsInstalled)
                                 ?? RecommendedLocalModels.FirstOrDefault(i => i.IsCompatible)
                                 ?? RecommendedLocalModels.FirstOrDefault();

            SelectedRemoteItem = AvailableRemoteModels.FirstOrDefault(i => i.Profile.Id == activeId)
                                 ?? AvailableRemoteModels.FirstOrDefault();

            StatusMessage = "Models and hardware tiers evaluated successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load models: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task DownloadModelAsync(LocalModelDisplayItem item)
    {
        if (item == null) return;

        if (!item.IsCompatible)
        {
            StatusMessage = $"Cannot download: Model requires more RAM than available ({item.CompatibilityReason}).";
            return;
        }

        item.IsDownloading = true;
        StatusMessage = $"Starting download for '{item.Model.DisplayName}'...";

        try
        {
            await _modelManager.DownloadAndInstallModelAsync(item.Model.Id);
            item.IsInstalled = true;
            item.IsDownloading = false;
            StatusMessage = $"Model '{item.Model.DisplayName}' downloaded and verified successfully!";
        }
        catch (OperationCanceledException)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download of '{item.Model.DisplayName}' was cancelled.";
        }
        catch (Exception ex)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task CancelDownloadAsync(LocalModelDisplayItem item)
    {
        if (item == null) return;
        await _modelManager.CancelDownloadAsync(item.Model.Id);
        item.IsDownloading = false;
        StatusMessage = $"Download cancellation requested for '{item.Model.DisplayName}'.";
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SaveSelectionAsync()
    {
        var current = _configService.Current;

        if (IsLocalMode)
        {
            if (SelectedLocalModel == null)
            {
                StatusMessage = "Please select a local model.";
                return;
            }

            if (!SelectedLocalModel.IsCompatible)
            {
                StatusMessage = $"Selection blocked: Incompatible with this PC ({SelectedLocalModel.CompatibilityReason}).";
                return;
            }

            if (!SelectedLocalModel.IsInstalled)
            {
                StatusMessage = $"Model '{SelectedLocalModel.Model.DisplayName}' is not installed. Please download it first.";
                return;
            }

            current.Llm.SelectedModelId = SelectedLocalModel.Model.Id;
            current.Llm.Provider = "LLamaSharp";

            // Pre-load local model into memory
            IsBusy = true;
            StatusMessage = $"Loading model '{SelectedLocalModel.Model.DisplayName}' into memory...";
            try
            {
                var filePath = _modelManager.GetModelFilePath(SelectedLocalModel.Model.Id);
                await _localProvider.LoadModelAsync(filePath, current.Llm);
                await _localProvider.WarmupAsync();
                _modelManager.MarkModelInUse(SelectedLocalModel.Model.Id, true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load local model: {ex.Message}";
                IsBusy = false;
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }
        else
        {
            if (SelectedRemoteItem == null)
            {
                StatusMessage = "Please select a remote model profile.";
                return;
            }

            current.Llm.SelectedModelId = SelectedRemoteItem.Profile.Id;
            current.Llm.Provider = SelectedRemoteItem.Profile.Provider;
            current.Llm.Endpoint = Endpoint;
            current.Llm.ApiKey = ApiKey;
        }

        var validation = await _configService.UpdateSettingsAsync(current);
        StatusMessage = validation.IsValid
            ? (IsLocalMode ? $"Local model '{SelectedLocalModel?.Model.DisplayName}' activated." : $"Remote provider '{SelectedRemoteItem?.Profile.Name}' activated.")
            : $"Validation error: {validation}";
    }

    private void OnDownloadProgressChanged(object? sender, ModelDownloadProgress p)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var item = RecommendedLocalModels.FirstOrDefault(i => i.Model.Id == p.ModelId);
            if (item != null)
            {
                item.IsDownloading = p.Percentage < 100.0 && p.Status == ModelStatus.Downloading;
                item.DownloadPercentage = p.Percentage;
                item.DownloadProgressText = p.FormattedProgress;
                item.DownloadSpeedText = p.FormattedSpeed;
                item.DownloadEtaText = $"ETA: {p.FormattedEta}";
                item.Status = p.Status;
            }
        });
    }

    private void OnModelStatusChanged(object? sender, LocalModel m)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var item = RecommendedLocalModels.FirstOrDefault(i => i.Model.Id == m.Id);
            if (item != null)
            {
                item.Status = m.Status;
                item.IsInstalled = m.Status is ModelStatus.Ready or ModelStatus.Loaded or ModelStatus.InUse;
                item.IsDownloading = m.Status == ModelStatus.Downloading;
            }
        });
    }
}
