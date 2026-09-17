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

            // Initialize active context (authoritative persistent state)
            var activeContext = _serviceProvider.GetRequiredService<IActiveContextService>();
            activeContext.InitializeAsync().GetAwaiter().GetResult();

            var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            desktop.MainWindow = mainWindow;

            if (desktop.Args?.Contains("--capture-screenshots") == true)
            {
                mainWindow.Opened += async (_, _) =>
                {
                    await ScreenCaptureRunner.RunAsync(desktop, mainWindow, mainVm);
                };
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                var engine = _serviceProvider?.GetService<IAutomationEngine>();
                if (engine != null)
                {
                    try
                    {
                        engine.StopAsync().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Suppress shutdown exceptions to ensure process exits cleanly
                    }
                }

                var localLlama = _serviceProvider?.GetService<LocalLlamaProvider>();
                if (localLlama != null)
                {
                    try
                    {
                        localLlama.UnloadModelAsync().GetAwaiter().GetResult();
                        localLlama.Dispose();
                    }
                    catch
                    {
                        // Suppress cleanup exceptions during process exit
                    }
                }

                if (_serviceProvider is IDisposable disp)
                {
                    try { disp.Dispose(); } catch { }
                }
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
        services.AddSingleton<DeviceService>();
        services.AddSingleton<IActiveContextService, ActiveContextService>();

        // Hardware & Models
        services.AddSingleton<IHardwareDetector, LinuxHardwareDetector>();
        services.AddSingleton<IModelCatalog, JsonModelCatalog>();
        services.AddSingleton<IModelDownloader, ModelDownloader>();
        services.AddSingleton<IModelManager, ModelManager>();
        services.AddSingleton<LocalLlamaProvider>();

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

            if (config.Llm.Provider.Equals("LLamaSharp", StringComparison.OrdinalIgnoreCase) ||
                config.Llm.Provider.Equals("local", StringComparison.OrdinalIgnoreCase) ||
                config.Llm.Provider.Equals("local-llama", StringComparison.OrdinalIgnoreCase))
            {
                if (!LocalLlamaProvider.IsHardwareSupported(out _))
                {
                    return new LlamaCppProvider(
                        httpClient: client,
                        endpoint: config.Llm.Endpoint,
                        modelId: config.Llm.SelectedModelId);
                }

                var localProvider = sp.GetRequiredService<LocalLlamaProvider>();
                var modelManager = sp.GetRequiredService<IModelManager>();

                if (!localProvider.IsModelLoaded && !string.IsNullOrWhiteSpace(config.Llm.SelectedModelId))
                {
                    var modelPath = modelManager.GetModelFilePath(config.Llm.SelectedModelId);
                    if (File.Exists(modelPath))
                    {
                        try
                        {
                            localProvider.LoadModelAsync(modelPath, config.Llm).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // Defer error handling to execution time
                        }
                    }
                }

                return localProvider;
            }

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

        // Security & Safety Policy Services
        services.AddSingleton<IGamePolicyService, GamePolicyService>();
        services.AddSingleton<IGameActivityGuard, GameActivityGuard>();

        // Automation Engine
        services.AddSingleton<IAutomationEngine>(sp =>
        {
            var deviceController = sp.GetRequiredService<IDeviceController>();
            var gameRegistry = sp.GetRequiredService<IGameRegistry>();
            var sessionRecorder = sp.GetRequiredService<SessionRecorder>();
            var configService = sp.GetRequiredService<IConfigurationService>();
            var policyService = sp.GetRequiredService<IGamePolicyService>();
            var activityGuard = sp.GetRequiredService<IGameActivityGuard>();

            return new AutomationEngine(
                deviceController,
                () => sp.GetRequiredService<ILlmProvider>(),
                gameRegistry,
                sessionRecorder,
                configService,
                policyService,
                activityGuard);
        });

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DeviceSelectionViewModel>();
        services.AddSingleton<ModelSelectionViewModel>();
        services.AddSingleton<GameSelectionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SplashViewModel>();
        services.AddSingleton<AiDecisionDetailsViewModel>();
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