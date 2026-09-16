using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Games;

public class TapTitans2DefinitionTests
{
    [Fact]
    public void Properties_AreProperlyConfigured()
    {
        var game = new TapTitans2Definition();

        game.Id.Should().Be("tap-titans-2");
        game.Name.Should().Be("Tap Titans 2");
        game.Version.Should().Be("1.0.0");
        game.BasePrompt.Should().NotBeNullOrWhiteSpace();
        game.AllowedActions.Should().Contain(ActionType.Tap);
        game.AllowedActions.Should().Contain(ActionType.Swipe);
        game.AllowedActions.Should().Contain(ActionType.Wait);
        game.Constraints.Should().NotBeEmpty();
        game.DefaultSettings.Options.Should().ContainKey("auto_upgrade_heroes");
    }

    [Fact]
    public void ForbiddenShopConstraints_CoverTopRightAndBottomRight()
    {
        var game = new TapTitans2Definition();
        var constraints = game.Constraints.Where(c => c.Type == ConstraintType.ForbiddenRegion).ToList();

        constraints.Should().HaveCount(2);

        // Top right shop icon check
        var topShop = constraints.First(c => c.Id.Contains("TOP"));
        topShop.Parameters.Should().BeOfType<NormalizedRect>();
        var rectTop = (NormalizedRect)topShop.Parameters!;
        rectTop.Contains(0.9, 0.05).Should().BeTrue(); // inside shop
        rectTop.Contains(0.5, 0.5).Should().BeFalse(); // gameplay center area
    }
}

