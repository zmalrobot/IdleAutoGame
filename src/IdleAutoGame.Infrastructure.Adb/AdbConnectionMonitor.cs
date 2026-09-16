using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Infrastructure.Adb;

/// <summary>
/// Monitors connection health and performs exponential backoff reconnects for wireless endpoints.
/// </summary>
public sealed class AdbConnectionMonitor
{
    private static readonly int[] BackoffSeconds = [2, 4, 8, 16, 32];
    private readonly IDeviceConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of <see cref="AdbConnectionMonitor"/>.
    /// </summary>
    public AdbConnectionMonitor(IDeviceConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    /// <summary>
    /// Attempts to re-establish a dropped wireless connection with exponential backoff up to 5 attempts.
    /// </summary>
    public async Task<bool> AttemptReconnectAsync(
        string host,
        int port,
        Func<int, int, Task>? onRetryAttempt = null,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < BackoffSeconds.Length; attempt++)
        {
            var delaySec = BackoffSeconds[attempt];

            if (onRetryAttempt != null)
            {
                await onRetryAttempt(attempt + 1, delaySec).ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            var connected = await _connectionManager.ConnectWirelessAsync(host, port, ct).ConfigureAwait(false);
            if (connected)
            {
                return true;
            }
        }

        return false;
    }
}

