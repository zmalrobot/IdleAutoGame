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
    private bool _isActive;

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

    public bool RequiresMmproj => Model.RequiresMmproj;

    public string MmprojBadge => Model.RequiresMmproj ? "+ mmproj" : "single GGUF";

    public string FormattedSize => $"{(Model.FileSize + (Model.RequiresMmproj ? Model.MmprojFileSize : 0)) / (1024.0 * 1024.0 * 1024.0):F2} GB";

    public string RamBadge => Model.RecommendedRamRange;

    public string QualityBadge => $"Qualità: {Model.QualityTier}";

    public string SpeedBadge => $"Velocità: {Model.SpeedTier}";

    public LocalModelDisplayItem(LocalModel model, bool isCompatible, string compatibilityReason, bool isInstalled, bool isActive = false)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _isCompatible = isCompatible;
        _compatibilityReason = compatibilityReason;
        _isInstalled = isInstalled;
        _status = model.Status;
        _isActive = isActive;
    }
}

public sealed partial class RemoteModelDisplayItem : ObservableObject
{
    public ModelProfile Profile { get; }
    public bool IsCompatible { get; }
    public string CompatibilityReason { get; }

    [ObservableProperty]
    private bool _isActive;

    public RemoteModelDisplayItem(ModelProfile profile, bool isCompatible, string compatibilityReason, bool isActive = false)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        IsCompatible = isCompatible;
        CompatibilityReason = compatibilityReason;
        _isActive = isActive;
    }
}

public partial class ModelSelectionViewModel : ViewModelBase
{
    private readonly IModelCatalog _catalog;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IConfigurationService _configService;
    private readonly IActiveContextService _activeContext;
    private readonly LocalLlamaProvider _localProvider;
    private readonly IExecutionStateGuard? _guard;

    [ObservableProperty]
    private bool _isExecutionLocked;

    [ObservableProperty]
    private bool _isLocalMode = true;

    [ObservableProperty]
    private string _activeModelDisplayName = "Nessuno";

    [ObservableProperty]
    private string _activeModelProvider = "None";

    [ObservableProperty]
    private string _activeModelStatus = "Non pronto";

    [ObservableProperty]
    private string _activeModelId = "None";

    [ObservableProperty]
    private bool _hasActiveModel;

    [ObservableProperty]
    private string _detectedRamText = "Rilevamento in corso...";

    [ObservableProperty]
    private string _detectedRamTierText = "RAM Tier: Balanced (16 GB)";

    [ObservableProperty]
    private ObservableCollection<LocalModelDisplayItem> _recommendedLocalModels = new();

    [ObservableProperty]
    private LocalModelDisplayItem? _selectedLocalModel;

    [ObservableProperty]
    private ObservableCollection<RemoteModelDisplayItem> _availableRemoteModels = new();

    [ObservableProperty]
    private RemoteModelDisplayItem? _selectedRemoteItem;

    [ObservableProperty]
    private string _endpoint = "http://localhost:8080";

    [ObservableProperty]
    private string? _apiKey;

    [ObservableProperty]
    private string _statusMessage = "Seleziona un modello AI da attivare.";

    [ObservableProperty]
    private bool _isBusy;

    public ModelSelectionViewModel(
        IModelCatalog catalog,
        IModelManager modelManager,
        IHardwareDetector hardwareDetector,
        IConfigurationService configService,
        IActiveContextService activeContext,
        LocalLlamaProvider localProvider,
        IExecutionStateGuard? guard = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _localProvider = localProvider ?? throw new ArgumentNullException(nameof(localProvider));
        _guard = guard;

        if (_guard != null)
        {
            _isExecutionLocked = _guard.IsExecutionLocked;
            _guard.StateChanged += (_, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsExecutionLocked = e.IsExecutionLocked;
                });
            };
        }

        _modelManager.DownloadProgressChanged += OnDownloadProgressChanged;
        _modelManager.ModelStatusChanged += OnModelStatusChanged;
        _activeContext.ContextChanged += OnActiveContextChanged;

        SyncFromActiveContext();
    }

    private void OnActiveContextChanged(object? sender, ActiveContextChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromActiveContext);
    }

    private void SyncFromActiveContext()
    {
        var active = _activeContext.ActiveModel;
        ActiveModelDisplayName = active.DisplayName;
        ActiveModelProvider = active.Provider;
        ActiveModelStatus = active.Status;
        ActiveModelId = active.ModelId;
        HasActiveModel = !string.IsNullOrWhiteSpace(active.ModelId) && active.ModelId != "None";

        foreach (var item in RecommendedLocalModels)
        {
            item.IsActive = string.Equals(item.Model.Id, active.ModelId, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var item in AvailableRemoteModels)
        {
            item.IsActive = string.Equals(item.Profile.Id, active.ModelId, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadModelsAsync()
    {
        IsBusy = true;
        StatusMessage = "Valutazione specifiche hardware e caricamento catalogo modelli...";

        try
        {
            var hardware = await _hardwareDetector.DetectAsync();
            DetectedRamText = $"{hardware.TotalRamMb / 1024.0:F1} GB RAM (Disponibile: {hardware.AvailableRamMb / 1024.0:F1} GB)";

            var tier = _catalog.DetermineRamTier(hardware);
            DetectedRamTierText = tier switch
            {
                RamTier.Tier8Gb => "RAM Tier: Entry (≤ 8 GB)",
                RamTier.Tier16Gb => "RAM Tier: Balanced (8–16 GB)",
                _ => "RAM Tier: Performance (16–32+ GB)"
            };

            var activeId = _activeContext.ActiveModel.ModelId;

            // 1. Load Recommended Local Models
            var recommended = _catalog.GetRecommendedModelsForTier(tier);
            RecommendedLocalModels.Clear();

            foreach (var lm in recommended)
            {
                var compatible = lm.IsCompatibleWith(hardware, out var reason);
                var installed = await _modelManager.IsModelInstalledAsync(lm.Id);
                var isActive = string.Equals(lm.Id, activeId, StringComparison.OrdinalIgnoreCase);
                var item = new LocalModelDisplayItem(lm, compatible, reason, installed, isActive);
                RecommendedLocalModels.Add(item);
            }

            // 2. Load Remote Models
            var allProfiles = _catalog.GetAllModels();
            AvailableRemoteModels.Clear();
            foreach (var p in allProfiles.Where(p => !p.IsLocal || p.Provider != "LLamaSharp"))
            {
                var compatible = p.IsCompatibleWith(hardware, out var reason);
                var isActive = string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase);
                AvailableRemoteModels.Add(new RemoteModelDisplayItem(p, compatible, reason, isActive));
            }

            var currentSettings = _configService.Current.Llm;
            Endpoint = currentSettings.Endpoint;
            ApiKey = currentSettings.ApiKey;

            IsLocalMode = currentSettings.Provider.Equals("LLamaSharp", StringComparison.OrdinalIgnoreCase) ||
                          currentSettings.Provider.Equals("local", StringComparison.OrdinalIgnoreCase);

            SelectedLocalModel = RecommendedLocalModels.FirstOrDefault(i => i.Model.Id == activeId)
                                 ?? RecommendedLocalModels.FirstOrDefault(i => i.IsCompatible && i.IsInstalled)
                                 ?? RecommendedLocalModels.FirstOrDefault(i => i.IsCompatible)
                                 ?? RecommendedLocalModels.FirstOrDefault();

            SelectedRemoteItem = AvailableRemoteModels.FirstOrDefault(i => i.Profile.Id == activeId)
                                 ?? AvailableRemoteModels.FirstOrDefault();

            StatusMessage = "Modelli e specifiche hardware caricati correttamente.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore caricamento modelli: {ex.Message}";
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
            StatusMessage = $"Download bloccato: memoria RAM insufficiente ({item.CompatibilityReason}).";
            return;
        }

        item.IsDownloading = true;
        StatusMessage = $"Download avviato per '{item.Model.DisplayName}'...";

        try
        {
            await _modelManager.DownloadAndInstallModelAsync(item.Model.Id);
            item.IsInstalled = true;
            item.IsDownloading = false;
            StatusMessage = $"Modello '{item.Model.DisplayName}' scaricato e verificato!";
        }
        catch (OperationCanceledException)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download di '{item.Model.DisplayName}' annullato.";
        }
        catch (Exception ex)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download fallito: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task CancelDownloadAsync(LocalModelDisplayItem item)
    {
        if (item == null) return;
        await _modelManager.CancelDownloadAsync(item.Model.Id);
        item.IsDownloading = false;
        StatusMessage = $"Richiesto annullamento download per '{item.Model.DisplayName}'.";
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SetActiveLocalModelItemAsync(LocalModelDisplayItem? item)
    {
        if (item == null) return;
        SelectedLocalModel = item;
        await SaveSelectionAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SetActiveRemoteModelItemAsync(RemoteModelDisplayItem? item)
    {
        if (item == null) return;
        SelectedRemoteItem = item;
        await SaveSelectionAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SaveSelectionAsync()
    {
        var current = _configService.Current;

        if (IsLocalMode)
        {
            if (SelectedLocalModel == null)
            {
                StatusMessage = "Seleziona un modello locale.";
                return;
            }

            if (_guard != null && _guard.IsExecutionLocked)
            {
                var check = _guard.CanChangeModel(SelectedLocalModel.Model.Id);
                if (!check.IsAllowed)
                {
                    StatusMessage = check.Message;
                    return;
                }
            }

            if (!SelectedLocalModel.IsCompatible)
            {
                StatusMessage = $"Attivazione bloccata: modello incompatibile con questo PC (Selection blocked: Incompatible - {SelectedLocalModel.CompatibilityReason}).";
                return;
            }

            if (!SelectedLocalModel.IsInstalled)
            {
                StatusMessage = $"Il modello '{SelectedLocalModel.Model.DisplayName}' non è ancora installato (is not installed). Scaricalo prima di attivarlo.";
                return;
            }

            current.Llm.SelectedModelId = SelectedLocalModel.Model.Id;
            current.Llm.Provider = "LLamaSharp";

            // Pre-load local model into memory
            IsBusy = true;
            StatusMessage = $"Caricamento in memoria di '{SelectedLocalModel.Model.DisplayName}'...";
            try
            {
                var filePath = _modelManager.GetModelFilePath(SelectedLocalModel.Model.Id);
                await _localProvider.LoadModelAsync(filePath, current.Llm);
                await _localProvider.WarmupAsync();
                _modelManager.MarkModelInUse(SelectedLocalModel.Model.Id, true);
            }
            catch (PlatformNotSupportedException ex)
            {
                current.Llm.Provider = "llama.cpp";
                if (string.IsNullOrWhiteSpace(current.Llm.Endpoint) || current.Llm.Endpoint.Contains("localhost") || current.Llm.Endpoint.Contains("127.0.0.1"))
                {
                    current.Llm.Endpoint = "http://127.0.0.1:8080";
                }
                _modelManager.MarkModelInUse(SelectedLocalModel.Model.Id, true);
                await _configService.UpdateSettingsAsync(current);
                await _activeContext.SetActiveModelAsync(SelectedLocalModel.Model.Id, "llama.cpp", current.Llm.Endpoint);
                StatusMessage = $"Modello attivato! Modalità server locale llama.cpp configurata ({current.Llm.Endpoint}) - {ex.Message}. (activated)";
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Impossibile caricare il modello locale: {ex.Message}";
                return;
            }
            finally
            {
                IsBusy = false;
            }

            await _configService.UpdateSettingsAsync(current);
            await _activeContext.SetActiveModelAsync(SelectedLocalModel.Model.Id, "LLamaSharp");
            StatusMessage = $"Modello locale '{SelectedLocalModel.Model.DisplayName}' attivato con successo! (activated)";
        }
        else
        {
            if (SelectedRemoteItem == null)
            {
                StatusMessage = "Seleziona un profilo remoto.";
                return;
            }

            if (_guard != null && _guard.IsExecutionLocked)
            {
                var check = _guard.CanChangeModel(SelectedRemoteItem.Profile.Id);
                if (!check.IsAllowed)
                {
                    StatusMessage = check.Message;
                    return;
                }
            }

            current.Llm.SelectedModelId = SelectedRemoteItem.Profile.Id;
            current.Llm.Provider = SelectedRemoteItem.Profile.Provider;
            current.Llm.Endpoint = Endpoint;
            current.Llm.ApiKey = ApiKey;
            await _configService.UpdateSettingsAsync(current);

            await _activeContext.SetActiveModelAsync(
                SelectedRemoteItem.Profile.Id,
                SelectedRemoteItem.Profile.Provider,
                Endpoint,
                ApiKey);

            StatusMessage = $"Provider remoto '{SelectedRemoteItem.Profile.Name}' attivato! (activated)";
        }
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
