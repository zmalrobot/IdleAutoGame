using System.Net;
using System.Text.Json;
using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Adb;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Infrastructure.Persistence;
using IdleAutoGame.Presentation.ViewModels;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Hardening;

/// <summary>
/// Comprehensive fault injection test suite for Release Candidate hardening.
/// Verifies resilience under simulated failures across ADB, LLM, Security Policies, Config, and Storage.
/// </summary>
public class FaultInjectionTests
{
    // =========================================================================
    // 1. ADB FAULT INJECTIONS
    // =========================================================================

    [Fact]
    public async Task AdbFault_ScreenshotThrowsSocketError_EngineAppliesErrorPolicyGracefully()
    {
        // Simulate ADB disconnection during screenshot acquisition
        var deviceController = Substitute.For<IDeviceController>();
        deviceController.GetScreenResolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Resolution(1080, 1920));
        deviceController.CaptureScreenshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ScreenshotData>(_ => throw new System.IO.IOException("ADB server socket closed unexpectedly"));

        var llmProvider = Substitute.For<ILlmProvider>();
        var gameRegistry = Substitute.For<IGameRegistry>();
        var game = Substitute.For<IGameDefinition>();
        game.Id.Returns("game-1");
        game.Name.Returns("Test Game");
        gameRegistry.GetById("game-1").Returns(game);

        var settings = new AppSettings();
        settings.Automation.EnableActivityGuard = false; // Isolate screenshot capture failure from activity check
        settings.Automation.ErrorPolicy = "pause";

        var sessionRepo = Substitute.For<ISessionRepository>();
        var sessionRecorder = new SessionRecorder(sessionRepo);

        using var engine = new AutomationEngine(
            deviceController,
            llmProvider,
            gameRegistry,
            sessionRecorder,
            settings);

        await engine.StartAsync("dev-1", "game-1", "model-1");

        // Wait brief interval to let background loop attempt capture and handle error policy
        await Task.Delay(400);

        engine.State.Should().Be(AutomationState.Paused);
        engine.PauseReason.Should().Contain("Error policy triggered");

        await engine.StopAsync();
    }

    [Fact]
    public async Task AdbFault_DeviceUnauthorized_DashboardBlocksStartWithActionableReason()
    {
        var engine = Substitute.For<IAutomationEngine>();
        var configService = Substitute.For<IConfigurationService>();
        var settings = new AppSettings();
        settings.Device.DefaultDeviceSerial = "unauth-device";
        configService.Current.Returns(settings);

        var gameRegistry = Substitute.For<IGameRegistry>();
        var discovery = Substitute.For<IDeviceDiscovery>();
        var controller = Substitute.For<IDeviceController>();
        var connManager = Substitute.For<IDeviceConnectionManager>();

        var unauthDev = new DeviceInfo
        {
            Serial = "unauth-device",
            DisplayName = "Pixel 8 Pro (Unauthorized)",
            State = DeviceState.Unauthorized
        };

        discovery.GetDevicesAsync().Returns([unauthDev]);
        discovery.GetDeviceDetailsAsync("unauth-device", Arg.Any<CancellationToken>()).Returns(unauthDev);

        var deviceService = new DeviceService(discovery, controller, connManager, configService);
        await deviceService.SelectDeviceAsync("unauth-device");

        var dashboardVm = new DashboardViewModel(engine, configService, gameRegistry, deviceService: deviceService);

        await dashboardVm.StartAutomationAsync();

        dashboardVm.PauseReason.Should().Contain("is Unauthorized");
        await engine.DidNotReceive().StartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AdbFault_NegativeTapCoordinates_ThrowsArgumentOutOfRangeException()
    {
        var controller = new AdbDeviceController();

        var act = () => controller.TapAsync("dev-1", -10, 50);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task AdbFault_NegativeSwipeCoordinates_ThrowsArgumentOutOfRangeException()
    {
        var controller = new AdbDeviceController();

        var act = () => controller.SwipeAsync("dev-1", 100, -20, 100, 500, 300);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task AdbFault_ReconnectThrowsSocketException_SafelyRetriesWithoutCrash()
    {
        var connManager = Substitute.For<IDeviceConnectionManager>();
        connManager.ConnectWirelessAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused));

        var monitor = new AdbConnectionMonitor(connManager);
        using var cts = new CancellationTokenSource(100);

        // Attempt reconnect with token cancellation to verify loop doesn't throw unhandled exception
        var result = await monitor.AttemptReconnectAsync("192.168.1.50", 5555, null, cts.Token);

        result.Should().BeFalse();
    }

    // =========================================================================
    // 2. LLM FAULT INJECTIONS
    // =========================================================================

    [Fact]
    public async Task LlmFault_Http500InternalServerError_OpenAiProviderReturnsGracefulError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "{\"error\": \"GPU out of memory\"}");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/v1/")
        };

        var provider = new OpenAiCompatibleProvider(httpClient, endpoint: "http://localhost:8080/v1");

        var response = await provider.AnalyzeAsync(new LlmRequest
        {
            SystemPrompt = "sys",
            UserPrompt = "usr",
            ScreenshotBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
        });

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("HTTP 500");
    }

    [Fact]
    public async Task LlmFault_EmptyChoicesResponse_OpenAiProviderReturnsSchemaError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"choices\": []}");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/v1/")
        };

        var provider = new OpenAiCompatibleProvider(httpClient, endpoint: "http://localhost:8080/v1");

        var response = await provider.AnalyzeAsync(new LlmRequest
        {
            SystemPrompt = "sys",
            UserPrompt = "usr",
            ScreenshotBase64 = "base64"
        });

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("Invalid OpenAI API response structure");
    }

    [Fact]
    public void LlmFault_EmptyResponse_ParserReturnsEmptyError()
    {
        var ok = LlmResponseParser.TryParse("", out var action, out var error);

        ok.Should().BeFalse();
        action.Should().BeNull();
        error.Should().Contain("content was empty");
    }

    [Fact]
    public void LlmFault_MalformedJsonSyntax_ParserReturnsMalformedError()
    {
        var ok = LlmResponseParser.TryParse("{ \"action\": \"tap\", \"explanation\": \"incomplete...", out var action, out var error);

        ok.Should().BeFalse();
        action.Should().BeNull();
        error.Should().Contain("Malformed JSON");
    }

    [Fact]
    public void LlmFault_MissingRequiredExplanation_ParserReturnsMissingError()
    {
        var json = "{ \"action\": \"tap\" }";
        var ok = LlmResponseParser.TryParse(json, out var action, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("explanation");
    }

    [Fact]
    public void LlmFault_UnknownActionType_ParserReturnsUnknownActionError()
    {
        var json = "{ \"action\": \"execute_arbitrary_shell\", \"explanation\": \"exploit\" }";
        var ok = LlmResponseParser.TryParse(json, out var action, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Invalid or unknown action type");
    }

    [Fact]
    public void LlmFault_ConversationalEmbeddedJson_ParserExtractsAndParsesSuccessfully()
    {
        var raw = "I examined the screen and decided to tap the main boss core.\n" +
                  "{\n" +
                  "  \"action\": \"tap\",\n" +
                  "  \"parameters\": { \"x\": 0.5, \"y\": 0.5 },\n" +
                  "  \"explanation\": \"Tap titan boss core\",\n" +
                  "  \"confidence\": 0.95,\n" +
                  "  \"game_state\": \"boss_fight\"\n" +
                  "}\n" +
                  "Hope this helps the player.";

        var ok = LlmResponseParser.TryParse(raw, out var action, out var error);

        ok.Should().BeTrue(error);
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.5);
        action.Explanation.Should().Be("Tap titan boss core");
        action.GameState.Should().Be(GameStateAssessment.BossFight);
    }

    [Fact]
    public void LlmFault_CoordinatesOutOfTolerance_ActionValidatorRejects()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Parameters = new ActionParameters { X = 1.25, Y = 0.5 }, // Exceeds tolerance 1.05
            Explanation = "Tap off screen"
        };

        var result = ActionValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*outside normalized range*");
    }

    [Fact]
    public void LlmFault_CoordinatesNanOrInfinity_ActionValidatorRejects()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Parameters = new ActionParameters { X = double.NaN, Y = 0.5 },
            Explanation = "Tap NaN"
        };

        var result = ActionValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*cannot be NaN or Infinity*");
    }

    // =========================================================================
    // 3. SECURITY & POLICY FAULT INJECTIONS
    // =========================================================================

    [Fact]
    public void PolicyFault_EvasivePromptAttemptingDiamondSpend_HeuristicDetectorBlocks()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal, // Evasive: LLM classified as Normal!
            Explanation = "Spend 500 diamonds to purchase legendary weapon chest",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };

        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*intent to spend premium currency*");
    }

    [Fact]
    public void PolicyFault_EvasivePromptAttemptingCreditPurchase_HeuristicDetectorBlocks()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal, // Evasive: LLM classified as Normal!
            Explanation = "Tap buy pack for real money via in-app purchase",
            Parameters = new ActionParameters { X = 0.8, Y = 0.2 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };

        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*intent to perform credit/real-money purchase*");
    }

    [Fact]
    public void PolicyFault_StoreTap_WhenCreditPurchaseDisabled_BlocksSpatialTap()
    {
        var storeConstraint = new GameConstraint(
            "STORE_BUTTON",
            "Main store entry button",
            ConstraintType.ForbiddenRegion,
            new NormalizedRect(0.8, 0.0, 0.2, 0.2));

        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal,
            Explanation = "Open store menu",
            Parameters = new ActionParameters { X = 0.9, Y = 0.1 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };

        var result = ActionPolicyValidator.Validate(action, policy, [storeConstraint]);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*restricted store/currency region*");
    }

    // =========================================================================
    // 4. CONFIGURATION & CRASH RECOVERY FAULT INJECTIONS
    // =========================================================================

    [Fact]
    public async Task ConfigFault_MalformedJsonFile_HealsOnDiskWithSafeDefaults()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Corrupt file contents
            await File.WriteAllTextAsync(tempFile, "{ \"General\": { \"Locale\": \"en\", CORRUPTED_DATA_TRUNCATED ");

            var repo = new JsonSettingsRepository(tempFile);
            var validator = new SettingsValidator();
            var service = new ConfigurationService(repo, validator);

            var settings = await service.InitializeAsync();

            settings.Should().NotBeNull();
            settings.Games.PerGame.Should().NotBeNull();
            // Verify sensitive policies are fail-safe false
            var defaultGame = settings.Games.PerGame.GetValueOrDefault("tap-titans-2", new GameSpecificSettings());
            defaultGame.AllowPremiumCurrency.Should().BeFalse();
            defaultGame.AllowCreditPurchases.Should().BeFalse();

            // Verify file on disk was healed with valid JSON
            var diskContent = await File.ReadAllTextAsync(tempFile);
            diskContent.Should().Contain("\"schemaVersion\"");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ConfigFault_NullGamesSection_ValidatorReportsError()
    {
        var settings = new AppSettings
        {
            Games = null!
        };

        var validator = new SettingsValidator();
        var result = validator.Validate(settings);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Games settings section cannot be null.");
    }

    // =========================================================================
    // 5. MODEL MANAGEMENT FAULT INJECTIONS
    // =========================================================================

    [Fact]
    public async Task ModelFault_DeleteInUseModel_ThrowsInvalidOperationException()
    {
        var catalog = Substitute.For<IModelCatalog>();
        var downloader = Substitute.For<IModelDownloader>();
        var manager = new ModelManager(catalog, downloader, storageDirectory: Path.GetTempPath());

        var model = new LocalModel
        {
            Id = "model-active",
            Name = "Active Model",
            DisplayName = "Active Model"
        };
        catalog.GetLocalModel("model-active").Returns(model);

        manager.MarkModelInUse("model-active", true);

        var act = () => manager.DeleteModelAsync("model-active");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*currently in use*");
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
