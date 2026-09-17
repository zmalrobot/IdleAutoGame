using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _title = "IdleAutoGame - Autonomous Android Game Automation";

    public DashboardViewModel Dashboard { get; }
    public DeviceSelectionViewModel Devices { get; }
    public ModelSelectionViewModel Models { get; }
    public GameSelectionViewModel Games { get; }
    public SettingsViewModel Settings { get; }
    public SplashViewModel Splash { get; }
    private readonly IdleAutoGame.Application.Services.IActiveContextService _activeContext;
    private readonly IdleAutoGame.Application.Services.IExecutionStateGuard? _executionGuard;

    [ObservableProperty]
    private string? _navigationNotice;

    [ObservableProperty]
    private bool _isExecutionLocked;

    [ObservableProperty]
    private string? _lockSummary;

    public bool CanNavigateToNonDashboard => !IsExecutionLocked;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        DeviceSelectionViewModel devices,
        ModelSelectionViewModel models,
        GameSelectionViewModel games,
        SettingsViewModel settings,
        SplashViewModel splash,
        IdleAutoGame.Application.Services.IActiveContextService activeContext,
        IdleAutoGame.Application.Services.IExecutionStateGuard? executionGuard = null)
    {
        Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        Devices = devices ?? throw new ArgumentNullException(nameof(devices));
        Models = models ?? throw new ArgumentNullException(nameof(models));
        Games = games ?? throw new ArgumentNullException(nameof(games));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Splash = splash ?? throw new ArgumentNullException(nameof(splash));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _executionGuard = executionGuard;

        if (_executionGuard != null)
        {
            _isExecutionLocked = _executionGuard.IsExecutionLocked;
            _lockSummary = _executionGuard.LockSummary;
            _executionGuard.StateChanged += OnExecutionStateChanged;
        }

        Splash.Ready += (_, _) => CurrentView = Dashboard;

        _currentView = Splash;
    }

    private void OnExecutionStateChanged(object? sender, IdleAutoGame.Application.Services.ApplicationRuntimeStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsExecutionLocked = e.IsExecutionLocked;
            LockSummary = _executionGuard?.LockSummary;
            OnPropertyChanged(nameof(CanNavigateToNonDashboard));

            if (e.IsExecutionLocked && CurrentView != Dashboard && CurrentView != Splash)
            {
                CurrentView = Dashboard;
            }
        });
    }

    [RelayCommand]
    public void DismissNotice()
    {
        NavigationNotice = null;
    }

    [RelayCommand]
    public void NavigateToDashboard()
    {
        NavigationNotice = null;
        CurrentView = Dashboard;
        _ = _activeContext.RefreshForegroundStatusAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task NavigateToDevicesAsync()
    {
        if (_executionGuard != null)
        {
            var check = _executionGuard.CanNavigateTo("Devices");
            if (!check.IsAllowed)
            {
                NavigationNotice = check.Message;
                return;
            }
        }

        NavigationNotice = null;
        CurrentView = Devices;
        await Devices.RefreshDevicesAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task NavigateToModelsAsync()
    {
        if (_executionGuard != null)
        {
            var check = _executionGuard.CanNavigateTo("Models");
            if (!check.IsAllowed)
            {
                NavigationNotice = check.Message;
                return;
            }
        }

        NavigationNotice = null;
        CurrentView = Models;
        await Models.LoadModelsAsync();
    }

    [RelayCommand]
    public void NavigateToGames()
    {
        if (_executionGuard != null)
        {
            var check = _executionGuard.CanNavigateTo("Games");
            if (!check.IsAllowed)
            {
                NavigationNotice = check.Message;
                return;
            }
        }

        NavigationNotice = null;
        CurrentView = Games;
        Games.LoadGames();
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        if (_executionGuard != null)
        {
            var check = _executionGuard.CanNavigateTo("Settings");
            if (!check.IsAllowed)
            {
                NavigationNotice = check.Message;
                return;
            }
        }

        NavigationNotice = null;
        CurrentView = Settings;
        Settings.LoadFromCurrent();
    }
}
