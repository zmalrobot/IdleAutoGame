using FluentAssertions;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Validation;

public class ActionValidationTests
{
    [Fact]
    public void SchemaValidator_ValidAction_Passes()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Valid explanation",
            Confidence = 0.9,
            GameState = GameStateAssessment.Normal
        };

        var result = SchemaValidator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SchemaValidator_EmptyExplanation_Fails()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "   ",
            Confidence = 0.8
        };

        var result = SchemaValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Explanation"));
    }

    [Fact]
    public void SchemaValidator_InvalidConfidence_Fails()
    {
        var action = new GameAction
        {
            Action = ActionType.Wait,
            Explanation = "Wait cycle",
            Confidence = 1.5
        };

        var result = SchemaValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Confidence"));
    }

    [Fact]
    public void ActionValidator_TapMissingCoordinates_Fails()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tap center",
            Parameters = new ActionParameters { X = 0.5, Y = null }
        };

        var result = ActionValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Tap requires both X and Y"));
    }

    [Fact]
    public void ActionValidator_LongPressMissingDuration_Fails()
    {
        var action = new GameAction
        {
            Action = ActionType.LongPress,
            Explanation = "Charge attack",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, DurationMs = null }
        };

        var result = ActionValidator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DurationMs"));
    }

    [Fact]
    public void ActionValidator_ClampsSlightlyOutOfBoundsCoordinates()
    {
        var parameters = new ActionParameters
        {
            X = -0.02,
            Y = 1.03,
            EndX = 0.5,
            EndY = 0.5
        };

        var clamped = ActionValidator.Clamp(parameters, out var wasClamped);

        wasClamped.Should().BeTrue();
        clamped.X.Should().Be(0.0);
        clamped.Y.Should().Be(1.0);
    }

    [Fact]
    public void PolicyValidator_ForbiddenRegionHit_Fails()
    {
        var forbiddenRect = new NormalizedRect(0.8, 0.0, 0.2, 0.2); // Top-right shop button
        var constraint = new GameConstraint("TT2-NO-SHOP", "Shop purchase zone is blocked", ConstraintType.ForbiddenRegion, forbiddenRect);

        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tap shop icon",
            Parameters = new ActionParameters { X = 0.85, Y = 0.05 }
        };

        var result = PolicyValidator.Validate(action, new[] { constraint });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("forbidden region"));
    }

    [Fact]
    public void PolicyValidator_UserOverrideDoNotTap_BlocksTap()
    {
        var userOverride = new UserOverride
        {
            Text = "Please do not tap during dialogue",
            IsActive = true
        };

        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tap anywhere",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var result = PolicyValidator.Validate(action, null, new[] { userOverride });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Blocked by user override"));
    }

    [Fact]
    public void ActionPipelineValidator_ValidFlow_ProducesClampedAction()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Tap main character",
            Parameters = new ActionParameters { X = 1.01, Y = 0.5 }
        };

        var result = ActionPipelineValidator.ValidateAndSanitize(action, null, null, out var sanitized);

        result.IsValid.Should().BeTrue();
        sanitized.Should().NotBeNull();
        sanitized!.Parameters.X.Should().Be(1.0);
        sanitized.Parameters.Y.Should().Be(0.5);
    }

    [Fact]
    public void ActionValidator_MultiTap_ValidatesCountAndIntervalBounds()
    {
        // Negative count
        var action1 = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Invalid count",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 0 }
        };
        ActionValidator.Validate(action1).IsValid.Should().BeFalse();

        // Excess count
        var action2 = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Too many taps",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 100 }
        };
        ActionValidator.Validate(action2).IsValid.Should().BeFalse();

        // Invalid interval
        var action3 = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Invalid interval",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 5, IntervalMs = 2 }
        };
        ActionValidator.Validate(action3).IsValid.Should().BeFalse();

        // Valid multi-tap
        var action4 = new GameAction
        {
            Action = ActionType.Tap,
            Explanation = "Valid multi-tap",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 10, IntervalMs = 50 }
        };
        ActionValidator.Validate(action4).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ActionValidator_Clamp_PreservesAndSanitizesCount()
    {
        var parameters = new ActionParameters
        {
            X = 0.5,
            Y = 0.5,
            Count = 80,
            IntervalMs = 50
        };

        var clamped = ActionValidator.Clamp(parameters, out bool wasClamped);
        wasClamped.Should().BeTrue();
        clamped.Count.Should().Be(50);
        clamped.IntervalMs.Should().Be(50);
    }
}

