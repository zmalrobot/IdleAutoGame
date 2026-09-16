using FluentAssertions;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Games.TapTitans2;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Registry;

public class GameRegistryTests
{
    [Fact]
    public void RegisterAndGetById_RetrievesGameDefinition()
    {
        var registry = new GameRegistry();
        var tt2 = new TapTitans2Definition();

        registry.Register(tt2);

        var retrieved = registry.GetById("TAP-TITANS-2");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Tap Titans 2");
    }

    [Fact]
    public void GetAll_ReturnsAllRegisteredGames()
    {
        var tt2 = new TapTitans2Definition();
        var registry = new GameRegistry([tt2]);

        var all = registry.GetAll();
        all.Should().ContainSingle();
        all[0].Id.Should().Be("tap-titans-2");
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        var registry = new GameRegistry();
        var result = registry.GetById("unknown-game");

        result.Should().BeNull();
    }
}

