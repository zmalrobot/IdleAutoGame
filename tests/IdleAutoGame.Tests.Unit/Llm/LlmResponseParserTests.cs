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
}

