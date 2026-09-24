using System.Text.Json;
using AdvancedSharpAdbClient;
using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Pipeline;
using IdleAutoGame.Application.Prompts;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Infrastructure.Adb;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;
using Xunit.Abstractions;

namespace IdleAutoGame.Tests.Unit.Adb;

public class LiveDeviceIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private const string TargetSerial = "aa59e6a2";

    public LiveDeviceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveDevice_ConnectedAndReady()
    {
        var adbClient = new AdbClient();
        var devices = adbClient.GetDevices();
        devices.Should().NotBeEmpty("Almeno un dispositivo fisico deve essere collegato via ADB");

        var targetDevice = devices.FirstOrDefault(d => d.Serial == TargetSerial) ?? devices.First();
        _output.WriteLine($"Dispositivo rilevato: {targetDevice.Serial} (Stato: {targetDevice.State})");
        targetDevice.State.Should().Be(AdvancedSharpAdbClient.Models.DeviceState.Online);

        var controller = new AdbDeviceController(adbClient);
        var resolution = await controller.GetScreenResolutionAsync(targetDevice.Serial);
        _output.WriteLine($"Risoluzione schermo: {resolution.Width}x{resolution.Height}");
        resolution.Width.Should().BeGreaterThan(0);
        resolution.Height.Should().BeGreaterThan(0);

        var foreground = await controller.GetForegroundAppAsync(targetDevice.Serial);
        _output.WriteLine($"App in primo piano: {foreground.PackageName} / {foreground.ActivityName}");
        if (!foreground.PackageName.Contains("taptitans2"))
        {
            var deviceData = new AdvancedSharpAdbClient.Models.DeviceData { Serial = targetDevice.Serial };
            var receiver = new AdvancedSharpAdbClient.Receivers.ConsoleOutputReceiver();
            await adbClient.ExecuteRemoteCommandAsync("monkey -p com.gamehivecorp.taptitans2 -c android.intent.category.LAUNCHER 1", deviceData, receiver, default);
            await Task.Delay(3000);
            foreground = await controller.GetForegroundAppAsync(targetDevice.Serial);
        }
        foreground.PackageName.Should().Contain("taptitans2", "Tap Titans 2 deve essere in esecuzione e in primo piano");
    }

    [Fact]
    public async Task LiveDevice_ScreenshotCaptureAndPipelineDownscaling()
    {
        var adbClient = new AdbClient();
        var controller = new AdbDeviceController(adbClient);

        // 1. Acquisizione screenshot reale ad alta risoluzione
        var rawScreenshot = await controller.CaptureScreenshotAsync(TargetSerial);
        rawScreenshot.Should().NotBeNull();
        rawScreenshot.ImageBytes.Should().NotBeEmpty();
        rawScreenshot.Width.Should().BeGreaterThan(0);
        rawScreenshot.Height.Should().BeGreaterThan(0);
        _output.WriteLine($"Screenshot grezzo catturato: {rawScreenshot.Width}x{rawScreenshot.Height} ({rawScreenshot.ImageBytes.Length / 1024} KB)");

        // 2. Elaborazione tramite ScreenshotPipeline (downscaling in-memory e conversione Base64)
        bool processed = ScreenshotPipeline.Process(rawScreenshot, out string base64Payload, out string? error);
        processed.Should().BeTrue($"Screenshot deve essere processato correttamente: {error}");
        base64Payload.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Screenshot processato e convertito in Base64 (lunghezza stringa: {base64Payload.Length})");
    }

    [Fact]
    public void LiveDevice_ModularMicroPromptAssembly_WithSessionState()
    {
        var game = new TapTitans2Definition();
        var sessionState = new SessionState
        {
            InitializationComplete = false,
            UpgradeCheckDue = true,
            FarmingBurstsSinceCheck = 0,
            LastBossResult = "none",
            LastActionSuccess = true,
            StuckCount = 0
        };

        var policy = new GamePolicy
        {
            AllowPremiumCurrency = false,
            AllowCreditPurchases = false
        };

        // Assembla prompt modulare per la fase di inizializzazione
        var systemPrompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState, policy: policy);
        systemPrompt.Should().Contain("MANDATORY STARTUP WORKFLOW");
        systemPrompt.Should().Contain("Sword Master");
        systemPrompt.Should().Contain("Heroes");
        systemPrompt.Should().Contain("Premium currency usage: DISABLED");
        systemPrompt.Should().Contain("Credit / real-money purchases: DISABLED");

        // Assembla user prompt con stato di sessione compatto
        var userPrompt = PromptBuilder.BuildUserPrompt(
            cycleNumber: 1,
            elapsed: TimeSpan.FromSeconds(5),
            sessionState: sessionState,
            allowPremiumCurrency: false,
            allowCreditPurchases: false);

        userPrompt.Should().Contain("SESSION STATE:");
        userPrompt.Should().Contain("\"initialization_complete\":false");
        userPrompt.Should().Contain("\"upgrade_check_due\":true");

        _output.WriteLine("System Prompt Modulare generato correttamente:");
        _output.WriteLine($"Lunghezza prompt di sistema: {systemPrompt.Length} caratteri (~{systemPrompt.Length / 4} token)");
        _output.WriteLine($"Lunghezza prompt utente: {userPrompt.Length} caratteri");
    }

    [Fact]
    public async Task LiveDevice_PhysicalTouchExecution_AndVisualVerification()
    {
        var adbClient = new AdbClient();
        var controller = new AdbDeviceController(adbClient);

        // 1. Acquisizione screenshot prima dell'azione
        var beforeShot = await controller.CaptureScreenshotAsync(TargetSerial);
        beforeShot.Should().NotBeNull();

        // 2. Esecuzione di un tocco di attacco sicuro nell'arena centrale dei titani (x: 0.50, y: 0.45)
        // Calcolo coordinate fisiche
        int targetX = (int)(beforeShot.Width * 0.50);
        int targetY = (int)(beforeShot.Height * 0.45);
        _output.WriteLine($"Esecuzione tocco fisico sul dispositivo a ({targetX}, {targetY})...");

        Func<Task> act = async () => await controller.TapAsync(TargetSerial, targetX, targetY);
        await act.Should().NotThrowAsync("Il comando di tocco ADB deve essere completato con successo");

        // Breve attesa per permettere al motore di gioco di registrare il frame
        await Task.Delay(400);

        // 3. Acquisizione screenshot dopo l'azione per verifica visiva
        var afterShot = await controller.CaptureScreenshotAsync(TargetSerial);
        afterShot.Should().NotBeNull();
        afterShot.ImageBytes.Length.Should().BeGreaterThan(0);

        // 4. Aggiornamento e verifica dello stato di sessione compatto
        var sessionState = new SessionState();
        sessionState.RecordActionResult(true);
        sessionState.RecordFarmingBurst();

        sessionState.LastActionSuccess.Should().BeTrue();
        sessionState.StuckCount.Should().Be(0);
        sessionState.FarmingBurstsSinceCheck.Should().Be(1);

        _output.WriteLine($"Verifica completata con successo! Stato di sessione: {sessionState.ToCompactJson(false, false)}");
    }

    [Fact]
    public async Task LiveDevice_RunActualLlmInferenceOnCurrentScreen()
    {
        var adbClient = new AdbClient();
        var controller = new AdbDeviceController(adbClient);

        var rawScreenshot = await controller.CaptureScreenshotAsync(TargetSerial);
        rawScreenshot.Should().NotBeNull();
        ScreenshotPipeline.Process(rawScreenshot, out string base64Payload, out _);

        var game = new TapTitans2Definition();
        var sessionState = new SessionState
        {
            InitializationComplete = false,
            UpgradeCheckDue = true
        };
        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };

        var systemPrompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState, policy: policy);
        var userPrompt = PromptBuilder.BuildUserPrompt(1, TimeSpan.FromSeconds(2), sessionState: sessionState);

        var modelPath = "/home/simone/.local/share/IdleAutoGame/models/qwen3-vl-8b-instruct.gguf";
        var llmSettings = new LlmSettings
        {
            SelectedModelId = "qwen3-vl-8b-instruct",
            Provider = "LLamaSharp",
            ContextSize = 16384,
            MaxTokens = 1024,
            Temperature = 0.2,
            UseGpu = true,
            ThreadCount = 8
        };

        using var provider = new LocalLlamaProvider();
        provider.StatusChanged += (s, e) => _output.WriteLine($"[LLM Status] {e.Phase}: {e.Message}");

        _output.WriteLine($"Loading model from {modelPath}...");
        await provider.LoadModelAsync(modelPath, llmSettings);
        _output.WriteLine($"Model loaded. MultimodalLoaded: {provider.IsMultimodalLoaded}, Backend: {provider.CurrentBackend}");

        var request = new LlmRequest
        {
            ScreenshotBase64 = base64Payload,
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.2,
            MaxTokens = 1024
        };

        _output.WriteLine("Starting inference cycle 1...");
        var response = await provider.AnalyzeAsync(request);

        _output.WriteLine($"Cycle 1 finished in {response.LatencyMs}ms. IsSuccess: {response.IsSuccess}");
        _output.WriteLine($"RawContent 1:\n{response.RawContent}");
        response.IsSuccess.Should().BeTrue($"Ciclo 1 deve avere successo: {response.Error}");

        _output.WriteLine("Starting inference cycle 2...");
        var response2 = await provider.AnalyzeAsync(request);
        _output.WriteLine($"Cycle 2 finished in {response2.LatencyMs}ms. IsSuccess: {response2.IsSuccess}");
        _output.WriteLine($"RawContent 2:\n{response2.RawContent}");
        response2.IsSuccess.Should().BeTrue($"Ciclo 2 deve avere successo: {response2.Error}");
    }
}
