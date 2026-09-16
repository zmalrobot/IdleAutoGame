using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Tests.Unit.Fakes;

public class FakeDeviceDiscovery : IDeviceDiscovery
{
    public List<DeviceInfo> Devices { get; set; } = new();

    public event EventHandler<DeviceDiscoveryEvent>? DeviceChanged;

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DeviceInfo>>(Devices);
    }

    public Task<DeviceInfo> GetDeviceDetailsAsync(string serial, CancellationToken ct = default)
    {
        var found = Devices.FirstOrDefault(d => d.Serial == serial);
        if (found != null)
        {
            return Task.FromResult(found with
            {
                State = DeviceState.Ready,
                ScreenResolution = new Resolution(1080, 2400),
                Density = 440,
                AndroidVersion = "14",
                Manufacturer = "Google",
                Model = "Pixel 8"
            });
        }

        return Task.FromResult(new DeviceInfo
        {
            Serial = serial,
            DisplayName = serial,
            State = DeviceState.Ready,
            ScreenResolution = new Resolution(1080, 2400)
        });
    }

    public void TriggerChange(DeviceInfo device, DeviceDiscoveryChangeType changeType)
    {
        DeviceChanged?.Invoke(this, new DeviceDiscoveryEvent(device, changeType));
    }
}

public class FakeDeviceController : IDeviceController
{
    public List<string> ExecutedCommands { get; } = new();
    public byte[] DummyScreenshotBytes { get; set; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public Resolution ScreenResolution { get; set; } = new(1080, 2400);
    public int ScreenDensity { get; set; } = 440;

    public Task<ScreenshotData> CaptureScreenshotAsync(string serial, CancellationToken ct = default)
    {
        ExecutedCommands.Add($"CaptureScreenshot({serial})");
        return Task.FromResult(new ScreenshotData
        {
            ImageBytes = DummyScreenshotBytes,
            Width = ScreenResolution.Width,
            Height = ScreenResolution.Height,
            CapturedAt = DateTimeOffset.UtcNow,
            DeviceSerial = serial
        });
    }

    public Task TapAsync(string serial, int x, int y, CancellationToken ct = default)
    {
        ExecutedCommands.Add($"Tap({serial}, {x}, {y})");
        return Task.CompletedTask;
    }

    public Task SwipeAsync(string serial, int x1, int y1, int x2, int y2, int durationMs = 300, CancellationToken ct = default)
    {
        ExecutedCommands.Add($"Swipe({serial}, {x1}, {y1}, {x2}, {y2}, {durationMs})");
        return Task.CompletedTask;
    }

    public Task LongPressAsync(string serial, int x, int y, int durationMs = 1000, CancellationToken ct = default)
    {
        ExecutedCommands.Add($"LongPress({serial}, {x}, {y}, {durationMs})");
        return Task.CompletedTask;
    }

    public Task BackAsync(string serial, CancellationToken ct = default)
    {
        ExecutedCommands.Add($"Back({serial})");
        return Task.CompletedTask;
    }

    public Task<Resolution> GetScreenResolutionAsync(string serial, CancellationToken ct = default)
    {
        return Task.FromResult(ScreenResolution);
    }

    public Task<int> GetScreenDensityAsync(string serial, CancellationToken ct = default)
    {
        return Task.FromResult(ScreenDensity);
    }
}

public class FakeDeviceConnectionManager : IDeviceConnectionManager
{
    public bool ConnectResult { get; set; } = true;
    public bool PairResult { get; set; } = true;
    public bool ResponsiveResult { get; set; } = true;
    public List<string> ConnectedEndpoints { get; } = new();

    public Task<bool> ConnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        if (ConnectResult)
        {
            ConnectedEndpoints.Add($"{host}:{port}");
        }
        return Task.FromResult(ConnectResult);
    }

    public Task<bool> PairWirelessAsync(string host, int port, string pairingCode, CancellationToken ct = default)
    {
        return Task.FromResult(PairResult);
    }

    public Task<bool> DisconnectWirelessAsync(string host, int port, CancellationToken ct = default)
    {
        ConnectedEndpoints.Remove($"{host}:{port}");
        return Task.FromResult(true);
    }

    public Task<bool> IsDeviceResponsiveAsync(string serial, CancellationToken ct = default)
    {
        return Task.FromResult(ResponsiveResult);
    }
}

