using System.Diagnostics;
using System.Net;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Receivers;
using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Infrastructure.Adb;

/// <summary>
/// Manages network connections, Android 11+ pairing, and responsiveness checks for ADB Wireless.
/// </summary>
public sealed class AdbConnectionManager : IDeviceConnectionManager
{
    private readonly IAdbClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="AdbConnectionManager"/>.
    /// </summary>
    public AdbConnectionManager(IAdbClient? client = null)
    {
        _client = client ?? new AdbClient();
    }

    /// <inheritdoc />
    public async Task<bool> ConnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        return await Task.Run(() =>
        {
            try
            {
                var endpoint = new DnsEndPoint(host, port);
                var result = _client.Connect(endpoint);
                return result.Contains("connected", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> PairWirelessAsync(string host, int port, string pairingCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(pairingCode);

        // Fallback to CLI process execution for Android 11+ ADB pairing protocol (ADR-003)
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"pair {host}:{port} {pairingCode}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                process.WaitForExit(10000);
                var output = process.StandardOutput.ReadToEnd();
                return output.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DisconnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        return await Task.Run(() =>
        {
            try
            {
                var endpoint = new DnsEndPoint(host, port);
                _client.Disconnect(endpoint);
                return true;
            }
            catch
            {
                return false;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsDeviceResponsiveAsync(string serial, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        var device = new AdvancedSharpAdbClient.Models.DeviceData { Serial = serial };

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _client.ExecuteRemoteCommandAsync("echo 1", device, receiver, ct).ConfigureAwait(false);
            return receiver.ToString().Trim().Contains('1');
        }
        catch
        {
            return false;
        }
    }
}
