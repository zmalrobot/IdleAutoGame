using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
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
        var outDir2 = Environment.GetEnvironmentVariable("SCREENSHOT_OUT_DIR");

        Directory.CreateDirectory(outDir1);
        if (!string.IsNullOrWhiteSpace(outDir2))
        {
            Directory.CreateDirectory(outDir2);
        }

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
            rtb.Save(path1);

            if (!string.IsNullOrWhiteSpace(outDir2))
            {
                var path2 = Path.Combine(outDir2, filename);
                rtb.Save(path2);
            }

            Console.WriteLine($"[CAPTURED] {filename} ({width}x{height})");
        }

        async Task CaptureSized(string filename, int customWidth, int customHeight)
        {
            var oldWidth = window.Width;
            var oldHeight = window.Height;
            window.Width = customWidth;
            window.Height = customHeight;
            await Task.Delay(450); // Allow layout recalculation

            using var rtb = new RenderTargetBitmap(new PixelSize(customWidth, customHeight), new Vector(96, 96));
            rtb.Render(window);

            var path1 = Path.Combine(outDir1, filename);
            rtb.Save(path1);

            if (!string.IsNullOrWhiteSpace(outDir2))
            {
                var path2 = Path.Combine(outDir2, filename);
                rtb.Save(path2);
            }

            Console.WriteLine($"[CAPTURED] {filename} ({customWidth}x{customHeight})");

            window.Width = oldWidth;
            window.Height = oldHeight;
            await Task.Delay(300);
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
            if (mainVm.Models.SelectedLocalModel != null)
            {
                mainVm.Models.SelectedLocalModel.IsActive = true;
                mainVm.Models.ActiveModelDisplayName = mainVm.Models.SelectedLocalModel.Model.DisplayName;
                mainVm.Models.ActiveModelProvider = "LLamaSharp (Local GGUF)";
                mainVm.Models.ActiveModelStatus = "Pronto";
                mainVm.Models.HasActiveModel = true;
            }
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

            var item1 = new DeviceDisplayItem(usbDev, true);
            var item2 = new DeviceDisplayItem(wifiDev, false);
            mainVm.Devices.Devices.Add(item1);
            mainVm.Devices.Devices.Add(item2);
            mainVm.Devices.SelectedDevice = item1;
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
            var unauthItem = new DeviceDisplayItem(unauthDev, false);
            mainVm.Devices.Devices.Add(unauthItem);
            mainVm.Devices.SelectedDevice = unauthItem;
            mainVm.Devices.StatusMessage = "Cannot select 'Android Emulator (Unauthorized)': device is Unauthorized. Please accept the RSA debugging prompt on the device screen.";
            await Capture("screen3c_device_selection_error.png");

            // -------------------------------------------------------------
            // SCREEN 4: Game Selection (Default deny-by-default vs modified)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 4: Game Selection...");
            mainVm.CurrentView = mainVm.Games;
            mainVm.Games.LoadGames();
            var tt2 = mainVm.Games.Games.FirstOrDefault(g => g.Game.Id == "tap-titans-2") ?? mainVm.Games.Games.FirstOrDefault();
            if (tt2 != null)
            {
                tt2.IsActive = true;
                mainVm.Games.SelectedGame = tt2;
                mainVm.Games.ActiveGameName = tt2.Game.Name;
                mainVm.Games.ActiveGamePackage = tt2.Game.ExpectedPackageName ?? "com.gamehivecorp.taptitans2";
                mainVm.Games.ActiveGameDetectionStatus = "In primo piano (com.unity3d.player.UnityPlayerActivity)";
                mainVm.Games.HasActiveGame = true;
            }

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
            mainVm.Dashboard.ActiveGamePackage = "com.gamehivecorp.taptitans2";
            mainVm.Dashboard.ActiveGameDetectionStatus = "In primo piano (com.unity3d.player.UnityPlayerActivity)";
            mainVm.Dashboard.ActiveGameIsForeground = true;
            mainVm.Dashboard.ActiveDeviceSerial = "ce5b878";
            mainVm.Dashboard.ActiveDeviceDisplayName = "Xiaomi 12T Pro (22081212UG)";
            mainVm.Dashboard.ActiveDeviceConnectionType = "USB";
            mainVm.Dashboard.ActiveDeviceStatus = "Ready";
            mainVm.Dashboard.ActiveDeviceIsConnected = true;
            mainVm.Dashboard.ActiveModelId = "gemma-4-e2b-it";
            mainVm.Dashboard.ActiveModelName = "Gemma 4 E2B (Edge Multimodal)";
            mainVm.Dashboard.ActiveModelProvider = "LLamaSharp (Local GGUF)";
            mainVm.Dashboard.ActiveModelStatus = "Pronto";
            mainVm.Dashboard.ActiveModelIsReady = true;
            mainVm.Dashboard.GuardIsValid = true;
            mainVm.Dashboard.GuardStatusText = "OK (Valido)";
            mainVm.Dashboard.GuardDetailText = "Rilevato: com.gamehivecorp.taptitans2/com.unity3d.player.UnityPlayerActivity | Atteso: com.gamehivecorp.taptitans2";
            mainVm.Dashboard.AgentStateText = "IDLE";
            mainVm.Dashboard.AgentDetailText = "In attesa di avvio automazione";
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
            mainVm.Dashboard.GuardIsValid = false;
            mainVm.Dashboard.GuardIsMismatch = true;
            mainVm.Dashboard.GuardStatusText = "PACKAGE NON CORRISPONDENTE";
            mainVm.Dashboard.GuardDetailText = "Rilevato: com.android.vending/AssetBrowserActivity | Atteso: com.gamehivecorp.taptitans2";
            mainVm.Dashboard.ActiveGameIsForeground = false;
            mainVm.Dashboard.ActiveGameDetectionStatus = "Non attivo (com.android.vending)";
            mainVm.Dashboard.AgentStateText = "ATTIVITÀ PERSA";
            mainVm.Dashboard.AgentDetailText = mainVm.Dashboard.PauseReason;
            await Capture("screen5c_dashboard_paused_alert.png");

            // 5d: Emergency Stopped State
            mainVm.Dashboard.State = AutomationState.Stopped;
            mainVm.Dashboard.PauseReason = "ARRESTO DI EMERGENZA ATTIVATO DALL'UTENTE. Tutti i task, token di inferenza e gesti ADB sono stati abortiti istantaneamente.";
            mainVm.Dashboard.AgentStateText = "ARRESTATO (EMERGENZA)";
            mainVm.Dashboard.AgentDetailText = mainVm.Dashboard.PauseReason;
            mainVm.Dashboard.LastActionType = "Tap (x: 540, y: 1280) × 10 (intervallo: 50ms)";
            await Capture("screen5d_dashboard_emergency_stopped.png");

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

            // -------------------------------------------------------------
            // SCREEN 7: Responsive Layout Tests (Multi-Resolution Verification)
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 7: Responsive Layout Tests...");
            mainVm.CurrentView = mainVm.Dashboard;
            await CaptureSized("screen7a_responsive_small_dashboard.png", 900, 600);
            await CaptureSized("screen7b_responsive_large_dashboard.png", 1400, 900);

            mainVm.CurrentView = mainVm.Settings;
            mainVm.Settings.SelectedTabIndex = 1; // LLM tab in small window
            await CaptureSized("screen7c_responsive_small_settings.png", 900, 600);

            // -------------------------------------------------------------
            // SCREEN 8: AI Decision Details Diagnostic Window
            // -------------------------------------------------------------
            Console.WriteLine("Capturing Screen 8: AI Decision Details Diagnostic Window...");
            var diagVm = mainVm.Dashboard.AiDecisionDetails;
            diagVm.ClearHistory();

            // Generate a realistic game screen render for the live screenshot viewport
            byte[] liveScreenshotBytes = Array.Empty<byte>();
            try
            {
                var gameCanvas = new Border
                {
                    Width = 540,
                    Height = 960,
                    Background = new SolidColorBrush(Color.Parse("#121820")),
                    Child = new Grid
                    {
                        RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                        Children =
                        {
                            new Border
                            {
                                Background = new SolidColorBrush(Color.Parse("#1B222D")),
                                Padding = new Thickness(16, 12),
                                Child = new DockPanel
                                {
                                    Children =
                                    {
                                        new TextBlock { Text = "Stage 14,820", Foreground = Brushes.Gold, FontWeight = FontWeight.Bold, FontSize = 14 },
                                        new TextBlock { Text = "Tap Titans 2", Foreground = Brushes.LightGray, FontSize = 12, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                                    }
                                }
                            },
                            new StackPanel
                            {
                                [Grid.RowProperty] = 1,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                Spacing = 14,
                                Children =
                                {
                                    new TextBlock { Text = "⚔️ TITAN BOSS", Foreground = Brushes.Crimson, FontWeight = FontWeight.ExtraBold, FontSize = 24, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                                    new TextBlock { Text = "Salute Titano: 45%", Foreground = Brushes.White, FontSize = 14, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                                    new Border
                                    {
                                        Width = 320, Height = 16, Background = new SolidColorBrush(Color.Parse("#3D1418")), CornerRadius = new CornerRadius(8),
                                        Child = new Border { Width = 144, Height = 16, Background = Brushes.Red, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, CornerRadius = new CornerRadius(8) }
                                    },
                                    new TextBlock { Text = "⏱️ 12.4s rimanenti", Foreground = Brushes.Orange, FontWeight = FontWeight.SemiBold, FontSize = 13, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                                    new Border
                                    {
                                        Background = new SolidColorBrush(Color.Parse("#202B38")), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8), Margin = new Thickness(0, 20, 0, 0),
                                        Child = new TextBlock { Text = "Zona Impatto: (540, 1280)", Foreground = Brushes.Cyan, FontSize = 12, FontFamily = "Consolas, monospace" }
                                    }
                                }
                            },
                            new Border
                            {
                                [Grid.RowProperty] = 2,
                                Background = new SolidColorBrush(Color.Parse("#161D26")),
                                Padding = new Thickness(14, 10),
                                Child = new TextBlock { Text = "Abilità: [Heavenly Strike ⚡] [Shadow Clone] [War Cry]", Foreground = Brushes.LightSkyBlue, FontSize = 11 }
                            }
                        }
                    }
                };
                gameCanvas.Measure(new Size(540, 960));
                gameCanvas.Arrange(new Rect(0, 0, 540, 960));

                using var placeholderRtb = new RenderTargetBitmap(new PixelSize(540, 960), new Vector(96, 96));
                placeholderRtb.Render(gameCanvas);
                using var ms = new MemoryStream();
                placeholderRtb.Save(ms);
                liveScreenshotBytes = ms.ToArray();
            }
            catch
            {
                // Fallback to empty if bitmap allocation fails
            }

            diagVm.AddDecision(new AiDecisionDetails
            {
                CycleNumber = 140,
                Timestamp = DateTime.UtcNow.AddSeconds(-8),
                ActionType = "Wait",
                ParametersSummary = "Attesa 1000ms",
                Confidence = 0.99,
                GameState = GameStateAssessment.Normal,
                ObservationSummary = "Transizione animazione tra ondate di nemici.",
                Objective = "Attesa completamento transizione grafica.",
                DecisionSummary = "Pausa breve di stabilizzazione.",
                SyntheticExplanation = "Nessuna azione offensiva o gestionale richiesta durante la transizione di livello.",
                PolicyStatus = "Verifica Policy: Azione Standard Consentita",
                ValidationPassed = true,
                ValidationErrors = Array.Empty<string>(),
                ActionExecuted = true,
                ExecutionResult = "Attesa 1000ms completata",
                LatencyMs = 95
            });

            diagVm.AddDecision(new AiDecisionDetails
            {
                CycleNumber = 141,
                Timestamp = DateTime.UtcNow.AddSeconds(-5),
                ActionType = "Tap",
                ParametersSummary = "(220.00, 1850.00)",
                Confidence = 0.98,
                GameState = GameStateAssessment.BossFight,
                ObservationSummary = "Icona abilità 'Heavenly Strike' pronta e carica nella barra inferiore.",
                Objective = "Attivazione abilità attiva Heavenly Strike per infliggere danno istantaneo massivo.",
                DecisionSummary = "Tap su abilità speciale slot 1.",
                SyntheticExplanation = "Abilità attiva disponibile durante la boss fight: trigger immediato approvato.",
                PolicyStatus = "Verifica Policy: Azione Standard Consentita",
                ValidationPassed = true,
                ValidationErrors = Array.Empty<string>(),
                ActionExecuted = true,
                ExecutionResult = "Skill attivata con successo",
                LatencyMs = 138,
                TargetX = 220,
                TargetY = 1850
            });

            diagVm.AddDecision(new AiDecisionDetails
            {
                CycleNumber = 142,
                Timestamp = DateTime.UtcNow.AddSeconds(-2),
                ActionType = "Tap",
                ParametersSummary = "(540.00, 1280.00) × 10 (intervallo: 50ms)",
                Confidence = 0.96,
                GameState = GameStateAssessment.BossFight,
                ObservationSummary = "Schermata di battaglia Boss: Titan Core esposto con barra vita al 45%. Timer del Boss attivo con 12s rimanenti.",
                Objective = "Attacco rapido a raffica (Multi-Tap) sul Titan Core per massimizzare il DPS prima della scadenza del timer.",
                DecisionSummary = "Esecuzione di una sequenza di 10 tap coordinati sull'area di impatto principale (540, 1280).",
                SyntheticExplanation = "Il modello ha identificato la fase critica del Boss. La strategia ottimale secondo la policy di gioco consiste nell'attivare una raffica di colpi manuali per sconfiggere il titano entro il tempo limite.",
                PolicyStatus = "Verifica Policy: Azione Standard Consentita",
                ValidationPassed = true,
                ValidationErrors = Array.Empty<string>(),
                ActionExecuted = true,
                ExecutionResult = "10 tap eseguiti con successo in 520ms",
                LatencyMs = 142,
                TargetX = 540,
                TargetY = 1280,
                Count = 10,
                RawResponse = "{\n  \"action\": \"tap\",\n  \"parameters\": {\n    \"x\": 540.0,\n    \"y\": 1280.0,\n    \"count\": 10,\n    \"interval_ms\": 50\n  },\n  \"confidence\": 0.96,\n  \"game_state\": \"boss_fight\",\n  \"observation_summary\": \"Titan Core esposto con barra vita al 45%.\",\n  \"objective\": \"Attacco rapido sul Titan Core.\",\n  \"explanation\": \"Sequenza rapida di 10 colpi coordinati.\"\n}"
            });

            diagVm.SelectedDecision = diagVm.Decisions.First();

            // Feed live streaming chunk for visual inspection of real-time terminal
            diagVm.OnLlmChunkReceived(null, new LlmOutputChunk
            {
                InferenceId = "live-capture-inference",
                State = LlmStreamState.Streaming,
                DeltaText = "{\n  \"action\": \"tap\",\n  \"parameters\": { \"x\": 540.0, \"y\": 1280.0, \"count\": 10 },\n  \"explanation\": \"In streaming dal modello...\"",
                AccumulatedText = "{\n  \"action\": \"tap\",\n  \"parameters\": { \"x\": 540.0, \"y\": 1280.0, \"count\": 10 },\n  \"explanation\": \"In streaming dal modello...\"",
                ChunkIndex = 14,
                TotalTokensSoFar = 86,
                TokensPerSecond = 34.8,
                ElapsedMs = 2470
            });
            diagVm.FlushBufferToUi();

            // Provide real-time latest screenshot to the diagnostic window
            if (liveScreenshotBytes.Length > 0)
            {
                diagVm.OnScreenshotCaptured(null, new ScreenshotData
                {
                    ImageBytes = liveScreenshotBytes,
                    Width = 1080,
                    Height = 2400,
                    CycleNumber = 142,
                    DeviceSerial = "emulator-5554 (Pixel 7 Pro)",
                    CapturedAt = DateTimeOffset.UtcNow
                });
            }

            var diagWindow = new AiDecisionDetailsWindow
            {
                DataContext = diagVm,
                Width = 1260,
                Height = 840
            };

            diagWindow.Show();
            await Task.Delay(500);

            var dWidth = (int)Math.Max(1260, diagWindow.Bounds.Width);
            var dHeight = (int)Math.Max(840, diagWindow.Bounds.Height);
            using (var rtbDiag = new RenderTargetBitmap(new PixelSize(dWidth, dHeight), new Vector(96, 96)))
            {
                rtbDiag.Render(diagWindow);
                var dPath1 = Path.Combine(outDir1, "screen8_ai_decision_details.png");
                rtbDiag.Save(dPath1);
                if (!string.IsNullOrWhiteSpace(outDir2))
                {
                    var dPath2 = Path.Combine(outDir2, "screen8_ai_decision_details.png");
                    rtbDiag.Save(dPath2);
                }
                Console.WriteLine($"[CAPTURED] screen8_ai_decision_details.png ({dWidth}x{dHeight})");
            }
            diagWindow.Close();

            Console.WriteLine("======================================================");
            Console.WriteLine("  All Visual Inspection & Responsive Screenshots Captured! ");
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

