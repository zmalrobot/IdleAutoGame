using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Infrastructure.Adb;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Infrastructure.Persistence;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IdleAutoGame.Presentation;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize configuration
            var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
            configService.InitializeAsync().GetAwaiter().GetResult();

            var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Persistence
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();

        // Application Services & Configuration
        services.AddSingleton<SettingsValidator>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<SessionRecorder>();

        // Hardware & Models
        services.AddSingleton<IHardwareDetector, LinuxHardwareDetector>();
        services.AddSingleton<IModelCatalog, JsonModelCatalog>();

        // ADB Layer
        services.AddSingleton<IDeviceDiscovery, AdbDeviceDiscovery>();
        services.AddSingleton<IDeviceController, AdbDeviceController>();
        services.AddSingleton<IDeviceConnectionManager, AdbConnectionManager>();

        // Games Layer
        services.AddSingleton<IGameRegistry>(sp => new GameRegistry([new TapTitans2Definition()]));

        // LLM Provider Factory
        services.AddSingleton(new HttpClient());
        services.AddTransient<ILlmProvider>(sp =>
        {
            var config = sp.GetRequiredService<IConfigurationService>().Current;
            var client = sp.GetRequiredService<HttpClient>();

            if (config.Llm.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase) ||
                config.Llm.Provider.Equals("openai-compatible", StringComparison.OrdinalIgnoreCase))
            {
                return new OpenAiCompatibleProvider(
                    httpClient: client,
                    endpoint: config.Llm.Endpoint,
                    apiKey: config.Llm.ApiKey,
                    modelId: config.Llm.SelectedModelId);
            }

            return new LlamaCppProvider(
                httpClient: client,
                endpoint: config.Llm.Endpoint,
                modelId: config.Llm.SelectedModelId);
        });

        // Automation Engine
        services.AddSingleton<IAutomationEngine>(sp =>
        {
            var deviceController = sp.GetRequiredService<IDeviceController>();
            var llmProvider = sp.GetRequiredService<ILlmProvider>();
            var gameRegistry = sp.GetRequiredService<IGameRegistry>();
            var sessionRecorder = sp.GetRequiredService<SessionRecorder>();
            var config = sp.GetRequiredService<IConfigurationService>().Current;

            return new AutomationEngine(deviceController, llmProvider, gameRegistry, sessionRecorder, config);
        });

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DeviceSelectionViewModel>();
        services.AddSingleton<ModelSelectionViewModel>();
        services.AddSingleton<GameSelectionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SplashViewModel>(sp =>
        {
            var detector = sp.GetRequiredService<IHardwareDetector>();
            var dashboard = sp.GetRequiredService<DashboardViewModel>();
            var mainVm = sp.GetService<MainWindowViewModel>();

            return new SplashViewModel(detector, onReady: () =>
            {
                if (mainVm != null)
                {
                    mainVm.CurrentView = dashboard;
                }
            });
        });

        services.AddSingleton<MainWindowViewModel>();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}