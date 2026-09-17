using FluentAssertions;
using IdleAutoGame.Application.Actions;
using IdleAutoGame.Application.Actions.Handlers;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Actions;

/// <summary>
/// Unit tests for all IActionHandler strategy implementations.
/// </summary>
public class ActionHandlerTests
{
    private readonly FakeDeviceController _device = new();
    private readonly AppSettings _settings = new();
    private readonly TapTitans2Definition _game = new();
    private const string Serial = "test-device";

    private ActionExecutionContext BuildContext(GameAction action, int width = 1080, int height = 2400)
    {
        return new ActionExecutionContext
        {
            DeviceSerial = Serial,
            Action = action,
            EffectiveResolution = new Resolution(width, height),
            Game = _game,
            Settings = _settings,
            DeviceController = _device,
            IsInterrupted = () => false
        };
    }

    // ─── TAP ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TapHandler_ExecutesTapAtNormalizedCoordinates()
    {
        var handler = new TapActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tap test",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.Contains("Tap") && c.Contains("540") && c.Contains("1200"));
    }

    [Fact]
    public async Task TapHandler_MissingCoordinates_ReturnsFailed()
    {
        var handler = new TapActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "No coords",
            Parameters = new ActionParameters()
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    // ─── MULTI-TAP ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultiTapHandler_ExecutesCorrectNumberOfTaps()
    {
        var handler = new MultiTapActionHandler();
        var action = new GameAction
        {
            Action = ActionType.MultiTap,
            Explanation = "Multi tap test",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 3, IntervalMs = 10 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Count(c => c.StartsWith("Tap")).Should().Be(3);
    }

    [Fact]
    public async Task MultiTapHandler_CancelledMidway_ReturnsCancel()
    {
        var handler = new MultiTapActionHandler();
        var action = new GameAction
        {
            Action = ActionType.MultiTap,
            Explanation = "Multi tap cancel test",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 10, IntervalMs = 5 }
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20); // Cancel very quickly
        var result = await handler.ExecuteAsync(BuildContext(action), cts.Token);

        // Either Cancelled or fewer taps than requested
        (result.Status == ActionResultStatus.Cancelled || _device.ExecutedCommands.Count(c => c.StartsWith("Tap")) < 10)
            .Should().BeTrue();
    }

    // ─── DOUBLE-TAP ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DoubleTapHandler_ExecutesTwoTaps()
    {
        var handler = new DoubleTapActionHandler();
        var action = new GameAction
        {
            Action = ActionType.DoubleTap,
            Explanation = "Double tap test",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, IntervalMs = 10 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        // DoubleTapAsync is called once (it internally does 2 taps)
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("DoubleTap"));
    }

    // ─── LONG-PRESS ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LongPressHandler_ExecutesLongPressAtCoordinates()
    {
        var handler = new LongPressActionHandler();
        var action = new GameAction
        {
            Action = ActionType.LongPress,
            Explanation = "Long press test",
            Parameters = new ActionParameters { X = 0.3, Y = 0.7, DurationMs = 800 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("LongPress") && c.Contains("800"));
    }

    // ─── SWIPE ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SwipeHandler_ExecutesSwipeBetweenCoordinates()
    {
        var handler = new SwipeActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Swipe,
            Explanation = "Swipe test",
            Parameters = new ActionParameters { X = 0.1, Y = 0.5, EndX = 0.9, EndY = 0.5, DurationMs = 300 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Swipe") && c.Contains("300"));
    }

    [Fact]
    public async Task SwipeHandler_MissingEndCoordinates_ReturnsFailed()
    {
        var handler = new SwipeActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Swipe,
            Explanation = "Swipe no end",
            Parameters = new ActionParameters { X = 0.1, Y = 0.5 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    // ─── DRAG ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DragHandler_ExecutesDragWithExtendedDuration()
    {
        var handler = new DragActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Drag,
            Explanation = "Drag test",
            Parameters = new ActionParameters { X = 0.2, Y = 0.3, EndX = 0.8, EndY = 0.7, DurationMs = 1500 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Drag") && c.Contains("1500"));
    }

    // ─── SCROLL ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrollHandler_ScrollDown_ExecutesSwipeWithDownCoordinates()
    {
        var handler = new ScrollActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Scroll,
            Explanation = "Scroll down test",
            Parameters = new ActionParameters { Direction = ScrollDirection.Down, Distance = 0.4 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Swipe"));
    }

    [Fact]
    public async Task ScrollHandler_MissingDirection_DefaultsToDownAndSucceeds()
    {
        var handler = new ScrollActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Scroll,
            Explanation = "Scroll no dir — uses default Down",
            Parameters = new ActionParameters()  // No direction → default Down
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeTrue();  // Handler defaults to ScrollDirection.Down
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Swipe"));
    }

    // ─── TEXT INPUT ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TextInputHandler_SendsSafeText()
    {
        var handler = new TextInputActionHandler();
        var action = new GameAction
        {
            Action = ActionType.TextInput,
            Explanation = "Text input test",
            Parameters = new ActionParameters { Text = "hello world" }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("SendText"));
    }

    [Theory]
    [InlineData("inject$this")]
    [InlineData("bad;command")]
    [InlineData("pipe|attack")]
    [InlineData("back`tick")]
    public async Task TextInputHandler_ShellInjectionAttempt_ReturnsFailed(string dangerousText)
    {
        var handler = new TextInputActionHandler();
        var action = new GameAction
        {
            Action = ActionType.TextInput,
            Explanation = "Injection test",
            Parameters = new ActionParameters { Text = dangerousText }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task TextInputHandler_NullOrEmptyText_ReturnsFailed()
    {
        var handler = new TextInputActionHandler();
        var action = new GameAction
        {
            Action = ActionType.TextInput,
            Explanation = "Empty text",
            Parameters = new ActionParameters { Text = null }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    // ─── KEY PRESS ────────────────────────────────────────────────────────────

    [Fact]
    public async Task KeyPressHandler_WhitelistedKey_Executes()
    {
        var handler = new KeyPressActionHandler();
        var action = new GameAction
        {
            Action = ActionType.KeyPress,
            Explanation = "Key press test",
            Parameters = new ActionParameters { KeyCode = AndroidKeyCode.Back }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("SendKeyEvent") && c.Contains("4")); // KEYCODE_BACK = 4
    }

    [Fact]
    public async Task KeyPressHandler_NullKeyCode_ReturnsFailed()
    {
        var handler = new KeyPressActionHandler();
        var action = new GameAction
        {
            Action = ActionType.KeyPress,
            Explanation = "Null key",
            Parameters = new ActionParameters { KeyCode = null }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    // ─── KEY SEQUENCE ─────────────────────────────────────────────────────────

    [Fact]
    public async Task KeySequenceHandler_SendsAllKeysInOrder()
    {
        var handler = new KeySequenceActionHandler();
        var action = new GameAction
        {
            Action = ActionType.KeySequence,
            Explanation = "Key sequence test",
            Parameters = new ActionParameters
            {
                KeyCodes = new List<AndroidKeyCode> { AndroidKeyCode.DpadUp, AndroidKeyCode.DpadDown, AndroidKeyCode.Enter },
                IntervalMs = 10
            }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Count(c => c.StartsWith("SendKeyEvent")).Should().Be(3);
    }

    // ─── NAVIGATION ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BackHandler_ExecutesBackCommand()
    {
        var handler = new BackActionHandler();
        var action = new GameAction { Action = ActionType.Back, Explanation = "Back" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Back"));
    }

    [Fact]
    public async Task HomeHandler_ExecutesHomeCommand()
    {
        var handler = new HomeActionHandler();
        var action = new GameAction { Action = ActionType.Home, Explanation = "Home" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Home"));
    }

    [Fact]
    public async Task RecentsHandler_ExecutesRecentsCommand()
    {
        var handler = new RecentsActionHandler();
        var action = new GameAction { Action = ActionType.Recents, Explanation = "Recents" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("Recents"));
    }

    // ─── SYSTEM CONTROLS ──────────────────────────────────────────────────────

    [Fact]
    public async Task VolumeUpHandler_ExecutesVolumeUp()
    {
        var handler = new VolumeUpActionHandler();
        var action = new GameAction { Action = ActionType.VolumeUp, Explanation = "Vol up" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("VolumeUp"));
    }

    [Fact]
    public async Task VolumeDownHandler_ExecutesVolumeDown()
    {
        var handler = new VolumeDownActionHandler();
        var action = new GameAction { Action = ActionType.VolumeDown, Explanation = "Vol down" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().ContainSingle(c => c.StartsWith("VolumeDown"));
    }

    // ─── WAIT / DO-NOTHING ────────────────────────────────────────────────────

    [Fact]
    public async Task WaitHandler_ReturnsSuccessWithoutTouchingDevice()
    {
        var handler = new WaitActionHandler();
        var action = new GameAction
        {
            Action = ActionType.Wait,
            Explanation = "Wait test",
            Parameters = new ActionParameters { DurationMs = 10 }
        };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task DoNothingHandler_ReturnsSuccessWithoutTouchingDevice()
    {
        var handler = new DoNothingActionHandler();
        var action = new GameAction { Action = ActionType.DoNothing, Explanation = "Do nothing" };
        var result = await handler.ExecuteAsync(BuildContext(action), CancellationToken.None);

        result.Success.Should().BeTrue();
        _device.ExecutedCommands.Should().BeEmpty();
    }
}
