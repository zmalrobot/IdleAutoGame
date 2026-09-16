using FluentAssertions;
using IdleAutoGame.Infrastructure.Adb;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Adb;

public class AdbConnectionMonitorTests
{
    [Fact]
    public async Task AttemptReconnectAsync_WhenReconnectFailsInitiallyThenSucceeds_ShouldReturnTrue()
    {
        // Arrange
        var fakeConnectionManager = new FakeDeviceConnectionManager();
        fakeConnectionManager.ConnectResult = false; // Initially failing
        var monitor = new AdbConnectionMonitor(fakeConnectionManager);

        var attempts = new List<int>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act with quick simulation
        var task = monitor.AttemptReconnectAsync("192.168.1.10", 5555, (attempt, delay) =>
        {
            attempts.Add(attempt);
            if (attempt == 1)
            {
                fakeConnectionManager.ConnectResult = true; // Succeed on retry
            }
            return Task.CompletedTask;
        }, cts.Token);

        // Cancel after testing to avoid long delays in test execution
        cts.CancelAfter(2500);
        var result = await task;

        // Assert
        result.Should().BeTrue();
        attempts.Should().Contain(1);
    }

    [Fact]
    public async Task AttemptReconnectAsync_WhenCancelled_ShouldReturnFalseSafely()
    {
        // Arrange
        var fakeConnectionManager = new FakeDeviceConnectionManager();
        fakeConnectionManager.ConnectResult = false;
        var monitor = new AdbConnectionMonitor(fakeConnectionManager);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Immediate cancel

        // Act
        var result = await monitor.AttemptReconnectAsync("192.168.1.10", 5555, ct: cts.Token);

        // Assert
        result.Should().BeFalse();
    }
}

