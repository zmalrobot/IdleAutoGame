using FluentAssertions;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Validation;

public class PolicyValidatorForbiddenRegionTests
{
    private readonly TapTitans2Definition _game = new();

    [Fact]
    public void FightBossButton_TapAtTopRight_IsValidAndNotBlocked()
    {
        // Real coordinates from 1080x2400 device: X ≈ 0.88, Y ≈ 0.11
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal,
            Explanation = "Tap COMBATTI IL BOSS button at top-right to initiate boss fight",
            Parameters = new ActionParameters { X = 0.88, Y = 0.11 }
        };

        var result = ActionPipelineValidator.ValidateAndSanitize(
            action,
            _game.Constraints,
            null,
            out var clampedAction,
            policy: GamePolicy.Default,
            game: _game);

        result.IsValid.Should().BeTrue("COMBATTI IL BOSS at top-right (0.88, 0.11) is a legitimate combat action and must never be blocked as a shop");
        clampedAction.Should().NotBeNull();
        clampedAction!.Parameters.X.Should().Be(0.88);
        clampedAction!.Parameters.Y.Should().Be(0.11);
    }

    [Fact]
    public void LeaveBossButton_TapAtTopRight_IsValidAndNotBlocked()
    {
        // Real coordinates during boss fight: "ABBANDONA LA BATTAGLIA" at X ≈ 0.88, Y ≈ 0.11
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal,
            Explanation = "Tap ABBANDONA LA BATTAGLIA button at top-right",
            Parameters = new ActionParameters { X = 0.88, Y = 0.11 }
        };

        var result = PolicyValidator.Validate(action, _game.Constraints, null, GamePolicy.Default);

        result.IsValid.Should().BeTrue("ABBANDONA LA BATTAGLIA must not be blocked");
    }

    [Theory]
    [InlineData(ActionType.Tap)]
    [InlineData(ActionType.MultiTap)]
    [InlineData(ActionType.DoubleTap)]
    [InlineData(ActionType.LongPress)]
    public void FloatingPromoOffer_TouchActionTypes_AreBlocked(ActionType actionType)
    {
        // Real coordinates of the "x8 VALUE!" floating diamond offer: X ≈ 0.92, Y ≈ 0.29
        var action = new GameAction
        {
            Action = actionType,
            Category = ActionCategory.Normal,
            Explanation = "Touch floating bundle offer on right edge",
            Parameters = new ActionParameters { X = 0.92, Y = 0.29, Count = 10 }
        };

        var result = PolicyValidator.Validate(action, _game.Constraints, null, GamePolicy.Default);

        result.IsValid.Should().BeFalse($"{actionType} on floating promotional offer must be blocked");
        result.Errors.Should().Contain(e => e.Contains("TT2-FORBIDDEN-PROMO-OFFER"));
    }

    [Theory]
    [InlineData(ActionType.Tap)]
    [InlineData(ActionType.MultiTap)]
    [InlineData(ActionType.DoubleTap)]
    [InlineData(ActionType.LongPress)]
    public void BottomShopTab_TouchActionTypes_AreBlocked(ActionType actionType)
    {
        // Real coordinates of bottom Tab 6 (Diamond Store): X ≈ 0.90, Y ≈ 0.95
        var action = new GameAction
        {
            Action = actionType,
            Category = ActionCategory.Normal,
            Explanation = "Touch bottom shop tab",
            Parameters = new ActionParameters { X = 0.90, Y = 0.95, Count = 5 }
        };

        var result = PolicyValidator.Validate(action, _game.Constraints, null, GamePolicy.Default);

        result.IsValid.Should().BeFalse($"{actionType} on bottom shop tab must be blocked");
        result.Errors.Should().Contain(e => e.Contains("TT2-FORBIDDEN-SHOP-BOTTOM"));
    }

    [Theory]
    [InlineData(ActionType.Swipe)]
    [InlineData(ActionType.Drag)]
    public void Gestures_StartingInForbiddenRegion_AreBlocked(ActionType gestureType)
    {
        var action = new GameAction
        {
            Action = gestureType,
            Category = ActionCategory.Normal,
            Explanation = "Gesture starting in bottom shop tab",
            Parameters = new ActionParameters { X = 0.90, Y = 0.95, EndX = 0.50, EndY = 0.50 }
        };

        var result = PolicyValidator.Validate(action, _game.Constraints, null, GamePolicy.Default);

        result.IsValid.Should().BeFalse($"{gestureType} starting in forbidden region must be blocked");
        result.Errors.Should().Contain(e => e.Contains("TT2-FORBIDDEN-SHOP-BOTTOM"));
    }

    [Theory]
    [InlineData(ActionType.Swipe)]
    [InlineData(ActionType.Drag)]
    public void Gestures_EndingInForbiddenRegion_AreBlocked(ActionType gestureType)
    {
        var action = new GameAction
        {
            Action = gestureType,
            Category = ActionCategory.Normal,
            Explanation = "Gesture ending in promo offer",
            Parameters = new ActionParameters { X = 0.50, Y = 0.50, EndX = 0.92, EndY = 0.29 }
        };

        var result = PolicyValidator.Validate(action, _game.Constraints, null, GamePolicy.Default);

        result.IsValid.Should().BeFalse($"{gestureType} ending in forbidden region must be blocked");
        result.Errors.Should().Contain(e => e.Contains("TT2-FORBIDDEN-PROMO-OFFER"));
    }
}
