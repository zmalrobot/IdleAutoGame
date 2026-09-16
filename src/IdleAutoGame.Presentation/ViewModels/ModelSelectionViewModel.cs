using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

public sealed record ModelDisplayItem(
    ModelProfile Profile,
    bool IsCompatible,
    string CompatibilityReason);

public partial class ModelSelectionViewModel : ViewModelBase
{
    private readonly IModelCatalog _catalog;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private ObservableCollection<ModelDisplayItem> _availableModels = new();

    [ObservableProperty]
    private ModelDisplayItem? _selectedItem;

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
        IHardwareDetector hardwareDetector,
        IConfigurationService configService)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [RelayCommand]
    public async Task LoadModelsAsync()
    {
        IsBusy = true;
        StatusMessage = "Evaluating hardware compatibility with available models...";

        try
        {
            var hardware = await _hardwareDetector.DetectAsync();
            var all = _catalog.GetAllModels();

            AvailableModels.Clear();
            foreach (var m in all)
            {
                var compatible = m.IsCompatibleWith(hardware, out var reason);
                AvailableModels.Add(new ModelDisplayItem(m, compatible, reason));
            }

            var currentSettings = _configService.Current.Llm;
            Endpoint = currentSettings.Endpoint;
            ApiKey = currentSettings.ApiKey;

            var activeId = currentSettings.SelectedModelId;
            SelectedItem = AvailableModels.FirstOrDefault(i => i.Profile.Id == activeId)
                           ?? AvailableModels.FirstOrDefault(i => i.IsCompatible)
                           ?? AvailableModels.FirstOrDefault();

            StatusMessage = "Models loaded successfully.";
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

    [RelayCommand]
    public async Task SaveSelectionAsync()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select a model.";
            return;
        }

        var current = _configService.Current;
        current.Llm.SelectedModelId = SelectedItem.Profile.Id;
        current.Llm.Provider = SelectedItem.Profile.Provider;
        current.Llm.Endpoint = Endpoint;
        current.Llm.ApiKey = ApiKey;

        var validation = await _configService.UpdateSettingsAsync(current);
        StatusMessage = validation.IsValid
            ? $"Model '{SelectedItem.Profile.Name}' configured as active."
            : $"Validation error: {validation}";
    }
}
