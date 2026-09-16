using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Presentation.Views;

namespace IdleAutoGame.Presentation;

/// <summary>
/// Automated visual inspection and screen capture runner for QA and verification.
/// Renders every screen and critical state at full fidelity (1100x700) and saves PNG artifacts.
/// </summary>
public static class ScreenCaptureRunner
{
    public static async Task RunAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window,
        MainWindowViewModel mainVm)
    {
        Console.WriteLine("======================================================");
        Console.WriteLine("  Starting Automated Visual Screen Capture Pipeline   ");
        Console.WriteLine("======================================================");

        var outDir1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/screenshots"));
        var outDir2 = "/home/simone/.gemini/antigravity/brain/c387fd6f-6fb7-4089-bc0d-775deec93fad/screenshots";

        Directory.CreateDirectory(outDir1);
        Directory.CreateDirectory(outDir2);

        // Ensure window layout is realized
        await Task.Delay(600);

        async Task Capture(string filename)
        {
            await Task.Delay(350); // Flush layout & render queue
            var width = (int)Math.Max(1100, window.Bounds.Width);
            var height = (int)Math.Max(700, window.Bounds.Height);

            using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            rtb.Render(window);

            var path1 = Path.Combine(outDir1, filename);
            var path2 = Path.Combine(outDir2, filename);

            rtb.Save(path1);
            rtb.Save(path2);

            Console.WriteLine($"[CAPTURED] {filename} ({width}x{height})");
        }

        try
        {
            // -------------------------------------------------------------
            // SCREEN 1: Splash / Preflight Screen
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 1: Splash / Preflight...");
            mainVm.CurrentView = mainVm.Splash;
            mainVm.Splash.StatusText = "Pre-flight checks passed successfully.";
            mainVm.Splash.IsCompleted = true;
            await Capture("screen1_splash.png");

            // -------------------------------------------------------------
            // SCREEN 2: Model Selection (Local, Remote, Downloading)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 2: Model Selection...");
            mainVm.CurrentView = mainVm.Models;
            await mainVm.Models.LoadModelsAsync();

            // 2a: Local Mode
            mainVm.Models.IsLocalMode = true;
            mainVm.Models.SelectedLocalModel = mainVm.Models.RecommendedLocalModels.FirstOrDefault();
            await Capture("screen2a_model_selection_local.png");

            // 2b: Remote Mode
            mainVm.Models.IsLocalMode = false;
            mainVm.Models.Endpoint = "https://api.openai.com/v1";
            mainVm.Models.ApiKey = "sk-live-simulated-key-42";
            mainVm.Models.SelectedRemoteItem = mainVm.Models.AvailableRemoteModels.FirstOrDefault();
            await Capture("screen2b_model_selection_remote.png");

            // 2c: Downloading state
            mainVm.Models.IsLocalMode = true;
            var targetModel = mainVm.Models.RecommendedLocalModels.FirstOrDefault();
            if (targetModel != null)
            {
                targetModel.IsDownloading = true;
                targetModel.DownloadPercentage = 68;
                targetModel.DownloadSpeedText = "14.2 MB/s";
                targetModel.DownloadEtaText = "00:45 remaining";
                targetModel.DownloadProgressText = "Downloading weights: 2.8 GB / 4.1 GB (68%)";
            }
            await Capture("screen2c_model_selection_downloading.png");
            if (targetModel != null) targetModel.IsDownloading = false;

            // -------------------------------------------------------------
            // SCREEN 3: Device Selection (List, Verified, Error)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 3: Device Selection...");
            mainVm.CurrentView = mainVm.Devices;
            mainVm.Devices.Devices.Clear();

            var usbDev = new DeviceInfo
            {
                Serial = "28211FDH20063Q",
                DisplayName = "Google Pixel 8 Pro",
                Manufacturer = "Google",
                Model = "Pixel 8 Pro",
                AndroidVersion = "14",
                ConnectionType = ConnectionType.USB,
                State = DeviceState.Ready,
                ScreenResolution = new Resolution(1080, 2400),
                Density = 480
            };

            var wifiDev = new DeviceInfo
            {
                Serial = "192.168.1.145:5555",
                DisplayName = "Samsung Galaxy S24 Ultra",
                Manufacturer = "Samsung",
                Model = "SM-S928B",
                AndroidVersion = "14",
                ConnectionType = ConnectionType.Wireless,
                State = DeviceState.Connected,
                ScreenResolution = new Resolution(1440, 3120),
                Density = 500,
                NetworkEndpoint = "192.168.1.145:5555"
            };

            mainVm.Devices.Devices.Add(usbDev);
            mainVm.Devices.Devices.Add(wifiDev);
            mainVm.Devices.SelectedDevice = usbDev;
            mainVm.Devices.WirelessHost = "192.168.1.145";
            mainVm.Devices.WirelessPort = 5555;
            mainVm.Devices.PairingCode = "642198";
            mainVm.Devices.StatusMessage = "Found 2 connected device(s). Google Pixel 8 Pro selected.";
            await Capture("screen3a_device_selection.png");

            // 3b: Device verified
            mainVm.Devices.StatusMessage = "Verification passed: Google Pixel 8 Pro is responsive and capturing frames (Screen: 1080x2400). Ready for automation.";
            await Capture("screen3b_device_selection_verified.png");

            // 3c: Device error / unauthorized fallback
            var unauthDev = new DeviceInfo
            {
                Serial = "emulator-5554",
                DisplayName = "Android Emulator (Unauthorized)",
                Manufacturer = "Google",
                Model = "sdk_gphone64_x86_64",
                AndroidVersion = "14",
                ConnectionType = ConnectionType.USB,
                State = DeviceState.Unauthorized,
                ScreenResolution = new Resolution(1080, 1920),
                Density = 420
            };
            mainVm.Devices.Devices.Add(unauthDev);
            mainVm.Devices.SelectedDevice = unauthDev;
            mainVm.Devices.StatusMessage = "Cannot select 'Android Emulator (Unauthorized)': device is Unauthorized. Please accept the RSA debugging prompt on the device screen.";
            await Capture("screen3c_device_selection_error.png");

            // -------------------------------------------------------------
            // SCREEN 4: Game Selection (Default deny-by-default vs modified)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 4: Game Selection...");
            mainVm.CurrentView = mainVm.Games;
            mainVm.Games.LoadGames();

            // 4a: Default (Deny by default)
            mainVm.Games.AllowPremiumCurrency = false;
            mainVm.Games.AllowCreditPurchases = false;
            mainVm.Games.StatusMessage = "Game 'Tap Titans 2' loaded. Security policies: Deny-By-Default active.";
            await Capture("screen4a_game_selection_default.png");

            // 4b: Policies modified / enabled
            mainVm.Games.AllowPremiumCurrency = true;
            mainVm.Games.AllowCreditPurchases = true;
            mainVm.Games.StatusMessage = "Selected game 'Tap Titans 2' and custom safety policies saved.";
            await Capture("screen4b_game_selection_policies_enabled.png");

            // -------------------------------------------------------------
            // SCREEN 5: Dashboard / Agent View (Idle, Running, Paused/Alert)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 5: Dashboard...");
            mainVm.CurrentView = mainVm.Dashboard;

            // 5a: Idle state
            mainVm.Dashboard.State = AutomationState.Idle;
            mainVm.Dashboard.ActiveGameName = "Tap Titans 2";
            mainVm.Dashboard.ActiveDeviceSerial = "28211FDH20063Q (Pixel 8 Pro)";
            mainVm.Dashboard.ActiveModelId = "Qwen2.5-VL-7B-Instruct (GGUF Q4_K_M)";
            mainVm.Dashboard.CyclesCount = 0;
            mainVm.Dashboard.ActionsCount = 0;
            mainVm.Dashboard.ErrorsCount = 0;
            mainVm.Dashboard.LastLatencyMs = 0;
            mainVm.Dashboard.PauseReason = null;
            await Capture("screen5a_dashboard_idle.png");

            // 5b: Active running state
            mainVm.Dashboard.State = AutomationState.Executing;
            mainVm.Dashboard.CyclesCount = 142;
            mainVm.Dashboard.ActionsCount = 139;
            mainVm.Dashboard.ErrorsCount = 0;
            mainVm.Dashboard.LastLatencyMs = 142;
            mainVm.Dashboard.LastActionType = "Tap (x: 540, y: 1280)";
            mainVm.Dashboard.LastGameState = GameStateAssessment.BossFight;
            mainVm.Dashboard.LastActionConfidence = 0.96;
            mainVm.Dashboard.LastActionExplanation = "Detected active boss encounter with 12s remaining on timer. Executing rapid hero tap attack on titan core.";

            mainVm.Dashboard.RecentCycles.Clear();
            mainVm.Dashboard.RecentCycles.Add(new CycleRecord
            {
                CycleNumber = 142,
                LlmLatencyMs = 142,
                Action = new GameAction
                {
                    Action = ActionType.Tap,
                    GameState = GameStateAssessment.BossFight,
                    Explanation = "Rapid tap attack on titan core",
                    Confidence = 0.96
                }
            });
            mainVm.Dashboard.RecentCycles.Add(new CycleRecord
            {
                CycleNumber = 141,
                LlmLatencyMs = 138,
                Action = new GameAction
                {
                    Action = ActionType.Tap,
                    GameState = GameStateAssessment.BossFight,
                    Explanation = "Rapid tap attack on titan core",
                    Confidence = 0.95
                }
            });
            mainVm.Dashboard.RecentCycles.Add(new CycleRecord
            {
                CycleNumber = 140,
                LlmLatencyMs = 155,
                Action = new GameAction
                {
                    Action = ActionType.Tap,
                    GameState = GameStateAssessment.BossFight,
                    Explanation = "Triggered Heavenly Strike active skill",
                    Confidence = 0.98
                }
            });
            mainVm.Dashboard.RecentCycles.Add(new CycleRecord
            {
                CycleNumber = 139,
                LlmLatencyMs = 120,
                Action = new GameAction
                {
                    Action = ActionType.Wait,
                    GameState = GameStateAssessment.Normal,
                    Explanation = "Waiting for boss defeat animation",
                    Confidence = 0.99
                }
            });
            mainVm.Dashboard.RecentCycles.Add(new CycleRecord
            {
                CycleNumber = 138,
                LlmLatencyMs = 140,
                Action = new GameAction
                {
                    Action = ActionType.Tap,
                    GameState = GameStateAssessment.Normal,
                    Explanation = "Level up Sword Master hero",
                    Confidence = 0.92
                }
            });

            mainVm.Dashboard.ActiveOverrides.Clear();
            mainVm.Dashboard.ActiveOverrides.Add(new UserOverride
            {
                Text = "Do not spend diamonds on chest",
                Scope = OverrideScope.Temporary,
                IsActive = true
            });
            mainVm.Dashboard.AllowPremiumCurrency = false;
            mainVm.Dashboard.AllowCreditPurchases = false;
            await Capture("screen5b_dashboard_active_cycle.png");

            // 5c: Paused / Alert State
            mainVm.Dashboard.State = AutomationState.ActivityLost;
            mainVm.Dashboard.PauseReason = "Android Activity Guard triggered: Foreground package changed from 'com.gamehivecorp.taptitans2' to 'com.android.vending'. Automation suspended immediately to prevent unauthorized purchases.";
            await Capture("screen5c_dashboard_paused_alert.png");

            // -------------------------------------------------------------
            // SCREEN 6: Settings (All 6 Tabs)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 6: Settings Tabs...");
            mainVm.CurrentView = mainVm.Settings;
            mainVm.Settings.LoadFromCurrent();

            // Tab 1: Generale
            mainVm.Settings.SelectedTabIndex = 0;
            await Capture("screen6a_settings_generale.png");

            // Tab 2: LLM / Modelli locali
            mainVm.Settings.SelectedTabIndex = 1;
            await Capture("screen6b_settings_llm.png");

            // Tab 3: Dispositivo / ADB
            mainVm.Settings.SelectedTabIndex = 2;
            await Capture("screen6c_settings_dispositivo.png");

            // Tab 4: Giochi e Policy
            mainVm.Settings.SelectedTabIndex = 3;
            await Capture("screen6d_settings_giochi_policy.png");

            // Tab 5: Logging e Audit
            mainVm.Settings.SelectedTabIndex = 4;
            await Capture("screen6e_settings_logging.png");

            // Tab 6: Interfaccia / Aspetto
            mainVm.Settings.SelectedTabIndex = 5;
            await Capture("screen6f_settings_interfaccia.png");

            Console.WriteLine("======================================================");
            Console.WriteLine("  All 18 Visual Inspection Screenshots Captured!      ");
            Console.WriteLine("======================================================");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Capture pipeline failed: {ex}");
        }
        finally
        {
            desktop.Shutdown(0);
        }
    }
}
