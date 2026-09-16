using FluentAssertions;
using IdleAutoGame.Application.Prompts;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Prompts;

public class PromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsSystemConstraintsAndGameRules()
    {
        var game = new TapTitans2Definition();
        var persistentInstruction = "Always save gold for clan boss.";

        var prompt = PromptBuilder.BuildSystemPrompt(game, persistentInstructions: persistentInstruction);

        prompt.Should().Contain("SYSTEM CONSTRAINTS");
        prompt.Should().Contain("Tap Titans 2");
        prompt.Should().Contain("Battle titans by tapping the middle active screen area");
        prompt.Should().Contain("Always save gold for clan boss.");
        prompt.Should().Contain("Allowed action primitives: Tap, Swipe, LongPress, Wait, DoNothing");
    }

    [Fact]
    public void BuildUserPrompt_IncludesCycleContextAndTemporaryOverrides()
    {
        var tempOverride = new UserOverride
        {
            Text = "Stop tapping until level 500",
            Scope = OverrideScope.Temporary,
            IsActive = true
        };

        var prevAction = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tapped boss weak point",
            Confidence = 0.95,
            GameState = GameStateAssessment.BossFight
        };

        var prompt = PromptBuilder.BuildUserPrompt(
            cycleNumber: 12,
            elapsed: TimeSpan.FromSeconds(45),
            previousAction: prevAction,
            userOverrides: [tempOverride]);

        prompt.Should().Contain("Stop tapping until level 500");
        prompt.Should().Contain("Cycle #12");
        prompt.Should().Contain("Session duration: 45.0s");
        prompt.Should().Contain("Previous action: Tap (Confidence: 95%)");
        prompt.Should().Contain("Previous game state: BossFight");
    }
}

