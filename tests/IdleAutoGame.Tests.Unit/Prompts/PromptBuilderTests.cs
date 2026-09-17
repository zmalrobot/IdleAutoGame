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
        prompt.Should().Contain("SCREEN REGIONS & COORDINATE MAPPING");
        prompt.Should().Contain("Always save gold for clan boss.");
        // AllowedActions now includes the full extended set
        prompt.Should().Contain("Allowed action primitives:");
        prompt.Should().Contain("Tap");
        prompt.Should().Contain("MultiTap");
        prompt.Should().Contain("Swipe");
        prompt.Should().Contain("Back");
        // New schema section must be present
        prompt.Should().Contain("\"multi_tap\":");
        prompt.Should().Contain("\"scroll\":");
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

    [Fact]
    public void BuildSystemPrompt_WhenGenericSystemPromptProvided_UsesCustomPrompt()
    {
        var game = new TapTitans2Definition();
        var customPrompt = "CUSTOM AGENT RULES: Focus only on active tapping and dismiss ads instantly.";

        var prompt = PromptBuilder.BuildSystemPrompt(game, genericSystemPrompt: customPrompt);

        prompt.Should().Contain(customPrompt);
        prompt.Should().Contain("### 1. SYSTEM CONSTRAINTS & ROLE INSTRUCTIONS");
        prompt.Should().Contain("Tap Titans 2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildSystemPrompt_WhenGenericSystemPromptNullOrEmpty_FallsBackToDefault(string? emptyPrompt)
    {
        var game = new TapTitans2Definition();

        var prompt = PromptBuilder.BuildSystemPrompt(game, genericSystemPrompt: emptyPrompt);

        prompt.Should().Contain("### 1. YOUR PURPOSE & ROLE");
        prompt.Should().Contain("### 2. DECISION HIERARCHY & SCREEN REASONING");
        prompt.Should().Contain("### 3. RESPONSE CONTRACT (STRICT JSON ONLY)");
        prompt.Should().Contain("### 4. AVAILABLE ACTIONS & PARAMETERS");
        prompt.Should().Contain("### 5. EXAMPLES OF VALID ACTIONS");
        prompt.Should().Contain("### 6. CONTEXT & DATA PROVIDED TO YOU");
    }

    [Fact]
    public void DefaultGenericSystemPrompt_ContainsAllRequiredActionPrimitivesAndSchemas()
    {
        var prompt = LlmSettings.DefaultGenericSystemPrompt;

        prompt.Should().Contain("\"tap\":");
        prompt.Should().Contain("\"multi_tap\":");
        prompt.Should().Contain("\"double_tap\":");
        prompt.Should().Contain("\"long_press\":");
        prompt.Should().Contain("\"swipe\":");
        prompt.Should().Contain("\"drag\":");
        prompt.Should().Contain("\"scroll\":");
        prompt.Should().Contain("\"text_input\":");
        prompt.Should().Contain("\"key_press\":");
        prompt.Should().Contain("\"key_sequence\":");
        prompt.Should().Contain("\"back\":");
        prompt.Should().Contain("\"wait\":");
        prompt.Should().Contain("\"do_nothing\":");

        prompt.Should().Contain("observation_summary");
        prompt.Should().Contain("objective");
        prompt.Should().Contain("decision_summary");
        prompt.Should().Contain("explanation");
    }
}

