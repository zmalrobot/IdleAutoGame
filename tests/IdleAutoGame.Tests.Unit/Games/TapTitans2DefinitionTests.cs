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
    public void ForbiddenShopConstraints_CoverPromoOfferAndBottomShop_AndLeaveBossButtonFree()
    {
        var game = new TapTitans2Definition();
        var constraints = game.Constraints.Where(c => c.Type == ConstraintType.ForbiddenRegion).ToList();

        constraints.Should().HaveCount(2);

        // Verify Boss Button at top-right (X ≈ 0.88, Y ≈ 0.11) is NOT blocked by any constraint
        foreach (var c in constraints)
        {
            var rect = (NormalizedRect)c.Parameters!;
            rect.Contains(0.88, 0.11).Should().BeFalse($"Boss button must not be blocked by constraint {c.Id}");
        }

        // Verify Floating Promo Offer (X ≈ 0.92, Y ≈ 0.29) is blocked
        var promo = constraints.First(c => c.Id.Contains("PROMO"));
        promo.Parameters.Should().BeOfType<NormalizedRect>();
        var rectPromo = (NormalizedRect)promo.Parameters!;
        rectPromo.Contains(0.92, 0.29).Should().BeTrue("Floating promo bundle offer should be inside forbidden region");
        rectPromo.Contains(0.5, 0.5).Should().BeFalse("Gameplay center area should not be blocked");

        // Verify Bottom Shop Tab (X ≈ 0.90, Y ≈ 0.95) is blocked
        var bottomShop = constraints.First(c => c.Id.Contains("BOTTOM"));
        bottomShop.Parameters.Should().BeOfType<NormalizedRect>();
        var rectBottom = (NormalizedRect)bottomShop.Parameters!;
        rectBottom.Contains(0.90, 0.95).Should().BeTrue("Bottom shop tab should be inside forbidden region");
        rectBottom.Contains(0.08, 0.95).Should().BeFalse("Sword master tab should not be blocked");
    }
}

