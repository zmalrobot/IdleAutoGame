using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Persistence;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Persistence;

public class SqliteSessionRepositoryTests : IDisposable
{
    private readonly string _tempDb;
    private readonly SqliteSessionRepository _repo;

    public SqliteSessionRepositoryTests()
    {
        _tempDb = Path.GetTempFileName();
        _repo = new SqliteSessionRepository(_tempDb);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDb))
        {
            try { File.Delete(_tempDb); } catch { }
        }
    }

    [Fact]
    public async Task SaveSessionAsync_InsertsAndRetrievesSession()
    {
        var session = new AutomationSession
        {
            Id = Guid.NewGuid(),
            GameId = "tap-titans-2",
            DeviceSerial = "usb-device-123",
            ModelId = "llava-7b",
            CycleCount = 42,
            ActionCount = 40,
            ErrorCount = 2,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            EndedAt = DateTimeOffset.UtcNow
        };

        await _repo.SaveSessionAsync(session);

        var recent = await _repo.GetRecentSessionsAsync(10);

        recent.Should().ContainSingle();
        var retrieved = recent[0];
        retrieved.Id.Should().Be(session.Id);
        retrieved.GameId.Should().Be("tap-titans-2");
        retrieved.DeviceSerial.Should().Be("usb-device-123");
        retrieved.CycleCount.Should().Be(42);
        retrieved.ActionCount.Should().Be(40);
        retrieved.ErrorCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveCycleAsync_InsertsAndRetrievesCycles()
    {
        var session = new AutomationSession
        {
            Id = Guid.NewGuid(),
            GameId = "tap-titans-2",
            DeviceSerial = "usb-device-123",
            ModelId = "llava-7b"
        };
        await _repo.SaveSessionAsync(session);

        var cycle = new CycleRecord
        {
            CycleNumber = 1,
            StartedAt = DateTimeOffset.UtcNow,
            Action = new GameAction
            {
                Action = ActionType.Tap,
                Parameters = new ActionParameters { X = 0.5, Y = 0.5 },
                Explanation = "Tap sword titan",
                Confidence = 0.98,
                GameState = GameStateAssessment.Normal
            },
            ValidationPassed = true,
            ActionExecuted = true,
            ExecutionResult = "Tapped (540, 960)",
            Duration = TimeSpan.FromMilliseconds(450),
            Errors = [],
            LlmLatencyMs = 210
        };

        await _repo.SaveCycleAsync(session.Id, cycle);

        var cycles = await _repo.GetSessionCyclesAsync(session.Id);

        cycles.Should().ContainSingle();
        var savedCycle = cycles[0];
        savedCycle.CycleNumber.Should().Be(1);
        savedCycle.Action.Should().NotBeNull();
        savedCycle.Action!.Action.Should().Be(ActionType.Tap);
        savedCycle.Action.Explanation.Should().Be("Tap sword titan");
        savedCycle.ValidationPassed.Should().BeTrue();
        savedCycle.ActionExecuted.Should().BeTrue();
        savedCycle.LlmLatencyMs.Should().Be(210);
    }
}

