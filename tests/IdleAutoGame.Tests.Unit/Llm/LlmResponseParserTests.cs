using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class LlmResponseParserTests
{
    [Fact]
    public void TryParse_ValidJson_ReturnsParsedAction()
    {
        var json = """
        {
          "action": "tap",
          "parameters": {
            "x": 0.5,
            "y": 0.8,
            "target": "UpgradeButton"
          },
          "explanation": "Upgrade the active sword hero.",
          "confidence": 0.95,
          "game_state": "normal",
          "wait_after_ms": 1500
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.5);
        action.Parameters.Y.Should().Be(0.8);
        action.Parameters.Target.Should().Be("UpgradeButton");
        action.Explanation.Should().Be("Upgrade the active sword hero.");
        action.Confidence.Should().Be(0.95);
        action.GameState.Should().Be(GameStateAssessment.Normal);
        action.WaitAfterMs.Should().Be(1500);
    }

    [Fact]
    public void TryParse_MarkdownCodeFences_StripsFencesAndParses()
    {
        var rawResponse = """
        Here is the decision:
        ```json
        {
          "action": "wait",
          "explanation": "Boss animation playing, waiting for shield to drop.",
          "confidence": 0.85,
          "game_state": "boss_fight",
          "wait_after_ms": 3000
        }
        ```
        Hope that helps!
        """;

        var ok = LlmResponseParser.TryParse(rawResponse, out var action, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Wait);
        action.GameState.Should().Be(GameStateAssessment.BossFight);
        action.WaitAfterMs.Should().Be(3000);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsFalseWithError()
    {
        var invalid = "This is not JSON at all.";

        var ok = LlmResponseParser.TryParse(invalid, out var action, out var error);

        ok.Should().BeFalse();
        action.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParse_MissingRequiredFields_ReturnsFalse()
    {
        var missingExplanation = """
        {
          "action": "tap",
          "confidence": 0.9
        }
        """;

        var ok = LlmResponseParser.TryParse(missingExplanation, out var action, out var error);

        ok.Should().BeFalse();
        action.Should().BeNull();
        error.Should().Contain("explanation");
    }

    [Fact]
    public void TryParse_MultiTapAndStructuredObservations_ParsesAllFields()
    {
        var json = """
        {
          "action": "tap",
          "parameters": {
            "x": 0.45,
            "y": 0.55,
            "count": 10,
            "interval_ms": 40
          },
          "explanation": "Rapid tap on boss monster weak point.",
          "confidence": 0.98,
          "game_state": "boss_fight",
          "observation_summary": "Boss Titan HP is low, timer at 12s, fairy floating top left.",
          "objective": "Defeat stage boss before timer expires.",
          "decision_summary": "Perform multi-tap sequence on central titan body."
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.45);
        action.Parameters.Y.Should().Be(0.55);
        action.Parameters.Count.Should().Be(10);
        action.Parameters.IntervalMs.Should().Be(40);
        action.GameState.Should().Be(GameStateAssessment.BossFight);
        action.ObservationSummary.Should().Be("Boss Titan HP is low, timer at 12s, fairy floating top left.");
        action.Objective.Should().Be("Defeat stage boss before timer expires.");
        action.DecisionSummary.Should().Be("Perform multi-tap sequence on central titan body.");
    }

    [Fact]
    public void TryParse_ScrollAction_ParsesDirectionAndDistance()
    {
        var json = """
        {
          "action": "scroll",
          "parameters": {
            "direction": "down",
            "distance": 0.4
          },
          "explanation": "Scroll down to see more heroes.",
          "confidence": 0.9,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.Scroll);
        action.Parameters.Direction.Should().Be(ScrollDirection.Down);
        action.Parameters.Distance.Should().Be(0.4);
    }

    [Fact]
    public void TryParse_DragAction_ParsesStartAndEnd()
    {
        var json = """
        {
          "action": "drag",
          "parameters": {
            "x": 0.3,
            "y": 0.2,
            "end_x": 0.7,
            "end_y": 0.8,
            "duration_ms": 1200
          },
          "explanation": "Drag hero card to slot.",
          "confidence": 0.88,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.Drag);
        action.Parameters.X.Should().Be(0.3);
        action.Parameters.Y.Should().Be(0.2);
        action.Parameters.EndX.Should().Be(0.7);
        action.Parameters.EndY.Should().Be(0.8);
        action.Parameters.DurationMs.Should().Be(1200);
    }

    [Fact]
    public void TryParse_DoubleTapAction_ParsesIntervalMs()
    {
        var json = """
        {
          "action": "double_tap",
          "parameters": {
            "x": 0.5,
            "y": 0.5,
            "interval_ms": 100
          },
          "explanation": "Double tap to activate skill.",
          "confidence": 0.92,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.DoubleTap);
        action.Parameters.X.Should().Be(0.5);
        action.Parameters.IntervalMs.Should().Be(100);
    }

    [Fact]
    public void TryParse_TextInputAction_ParsesText()
    {
        var json = """
        {
          "action": "text_input",
          "parameters": {
            "text": "hello"
          },
          "explanation": "Enter player name.",
          "confidence": 0.8,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.TextInput);
        action.Parameters.Text.Should().Be("hello");
    }

    [Fact]
    public void TryParse_KeyPressAction_ParsesKeyCodeByName()
    {
        var json = """
        {
          "action": "key_press",
          "parameters": {
            "key_code": "back"
          },
          "explanation": "Press back to dismiss dialog.",
          "confidence": 0.95,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.KeyPress);
        action.Parameters.KeyCode.Should().Be(AndroidKeyCode.Back);
    }

    [Fact]
    public void TryParse_KeyPressAction_ParsesKeyCodeWithPrefix()
    {
        var json = """
        {
          "action": "key_press",
          "parameters": {
            "key_code": "KEYCODE_ENTER"
          },
          "explanation": "Press enter.",
          "confidence": 0.9,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Parameters.KeyCode.Should().Be(AndroidKeyCode.Enter);
    }

    [Fact]
    public void TryParse_KeySequenceAction_ParsesKeyCodesArray()
    {
        var json = """
        {
          "action": "key_sequence",
          "parameters": {
            "key_codes": ["dpad_up", "dpad_down", "enter"],
            "interval_ms": 80
          },
          "explanation": "Navigate menu.",
          "confidence": 0.87,
          "game_state": "menu"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.KeySequence);
        action.Parameters.KeyCodes.Should().HaveCount(3);
        action.Parameters.KeyCodes![0].Should().Be(AndroidKeyCode.DpadUp);
        action.Parameters.KeyCodes[1].Should().Be(AndroidKeyCode.DpadDown);
        action.Parameters.KeyCodes[2].Should().Be(AndroidKeyCode.Enter);
        action.Parameters.IntervalMs.Should().Be(80);
    }

    [Fact]
    public void TryParse_BackAction_Parses()
    {
        var json = """
        {
          "action": "back",
          "explanation": "Press back button.",
          "confidence": 0.99,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Action.Should().Be(ActionType.Back);
    }

    [Fact]
    public void TryParse_CamelCaseEndXEndY_ParsesSuccessfully()
    {
        var json = """
        {
          "action": "swipe",
          "parameters": {
            "x": 0.1,
            "y": 0.5,
            "endX": 0.9,
            "endY": 0.5,
            "durationMs": 350
          },
          "explanation": "Swipe across screen.",
          "confidence": 0.9,
          "game_state": "normal"
        }
        """;

        var ok = LlmResponseParser.TryParse(json, out var action, out _);

        ok.Should().BeTrue();
        action!.Parameters.EndX.Should().Be(0.9);
        action.Parameters.EndY.Should().Be(0.5);
        action.Parameters.DurationMs.Should().Be(350);
    }

    [Fact]
    public void TryParse_WithThinkingTags_StripsThinkingAndParsesAction()
    {
        var raw = """
        <think>
        Looking at the screen, heroes tab is open.
        Sophia costs 800 gold. Current gold is 33.45K.
        Button is located at x=0.85, y=0.76.
        </think>
        ```json
        {
          "action": "tap",
          "parameters": {
            "x": 0.85,
            "y": 0.76,
            "target": "Sophia Arruola"
          },
          "game_state": "menu",
          "confidence": 0.95,
          "decision_summary": "Recruit Sophia"
        }
        ```
        """;

        var ok = LlmResponseParser.TryParse(raw, out var action, out var error);

        ok.Should().BeTrue(error);
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.85);
        action.Parameters.Y.Should().Be(0.76);
        action.Parameters.Target.Should().Be("Sophia Arruola");
        action.Explanation.Should().Be("Recruit Sophia");
    }



    [Fact]
    public void TryParse_WithNextActionAndStateAliases_ParsesActionAndState()
    {
        var raw = """
        {
          "next_action": "tap",
          "target": {
            "x": 0.72,
            "y": 0.41,
            "name": "UpgradeButton"
          },
          "state": "menu",
          "confidence": 0.94,
          "decision_summary": "Tap upgrade button"
        }
        """;

        var ok = LlmResponseParser.TryParse(raw, out var action, out var error);

        ok.Should().BeTrue(error);
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.72);
        action.Parameters.Y.Should().Be(0.41);
        action.Parameters.Target.Should().Be("UpgradeButton");
        action.GameState.Should().Be(GameStateAssessment.Menu);
    }

    [Fact]
    public void TryParse_MultipleSequentialJsonObjects_ExtractsActionObject()
    {
        var raw = """
        {
          "target_found": true,
          "target_name": "Sword Master tab",
          "x": 0.12,
          "y": 0.88,
          "confidence": 0.98
        }
        {
          "authorized": true,
          "currency_type": "normal",
          "rejection_action": "null"
        }
        {
          "action": "tap",
          "parameters": {
            "x": 0.12,
            "y": 0.88,
            "target": "Sword Master tab"
          },
          "explanation": "Open Sword Master tab to initiate startup sequence.",
          "confidence": 0.99
        }
        """;

        var ok = LlmResponseParser.TryParse(raw, out var action, out var error);

        ok.Should().BeTrue(error);
        action.Should().NotBeNull();
        action!.Action.Should().Be(ActionType.Tap);
        action.Parameters.X.Should().Be(0.12);
        action.Parameters.Y.Should().Be(0.88);
        action.Parameters.Target.Should().Be("Sword Master tab");
        action.Explanation.Should().Be("Open Sword Master tab to initiate startup sequence.");
    }
}


