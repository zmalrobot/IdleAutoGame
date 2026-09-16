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

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        DeviceSelectionViewModel devices,
        ModelSelectionViewModel models,
        GameSelectionViewModel games,
        SettingsViewModel settings,
        SplashViewModel splash)
    {
        Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        Devices = devices ?? throw new ArgumentNullException(nameof(devices));
        Models = models ?? throw new ArgumentNullException(nameof(models));
        Games = games ?? throw new ArgumentNullException(nameof(games));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Splash = splash ?? throw new ArgumentNullException(nameof(splash));
        Splash.Ready += (_, _) => CurrentView = Dashboard;

        _currentView = Splash;
    }

    [RelayCommand]
    public void NavigateToDashboard() => CurrentView = Dashboard;

    [RelayCommand]
    public async Task NavigateToDevicesAsync()
    {
        CurrentView = Devices;
        await Devices.RefreshDevicesAsync();
    }

    [RelayCommand]
    public async Task NavigateToModelsAsync()
    {
        CurrentView = Models;
        await Models.LoadModelsAsync();
    }

    [RelayCommand]
    public void NavigateToGames()
    {
        CurrentView = Games;
        Games.LoadGames();
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        CurrentView = Settings;
        Settings.LoadFromCurrent();
    }
}
