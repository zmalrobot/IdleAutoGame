---
title: ADB Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# ADB Architecture

## Overview
The ADB layer provides device discovery, connection management, and command execution for Android devices connected via USB or network (ADB Wireless). It implements Core interfaces, completely encapsulating transport differences.

## Core Interfaces Implemented

### IDeviceDiscovery
```csharp
public interface IDeviceDiscovery
{
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);
    IObservable<DeviceDiscoveryEvent> DeviceChanges { get; }
    Task<DeviceInfo> GetDeviceDetailsAsync(string serial, CancellationToken ct = default);
}
```

### IDeviceController
```csharp
public interface IDeviceController
{
    Task<ScreenshotData> CaptureScreenshotAsync(string serial, CancellationToken ct = default);
    Task TapAsync(string serial, int x, int y, CancellationToken ct = default);
    Task SwipeAsync(string serial, int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct = default);
    Task LongPressAsync(string serial, int x, int y, int durationMs, CancellationToken ct = default);
    Task BackAsync(string serial, CancellationToken ct = default);
    Task<Resolution> GetScreenResolutionAsync(string serial, CancellationToken ct = default);
    Task<int> GetScreenDensityAsync(string serial, CancellationToken ct = default);
}
```

### IDeviceConnectionManager
```csharp
public interface IDeviceConnectionManager
{
    Task ConnectWirelessAsync(string host, int port, CancellationToken ct = default);
    Task PairAsync(string host, int port, string pairingCode, CancellationToken ct = default);
    Task DisconnectAsync(string serial, CancellationToken ct = default);
    Task<bool> IsReachableAsync(string serial, CancellationToken ct = default);
}
```

## Implementation: AdbDeviceDiscovery
- Uses `AdvancedSharpAdbClient.AdbClient` to query the ADB server.
- `GetDevicesAsync()`: Calls `adb devices -l` equivalent, parses output, enriches with `getprop` calls for manufacturer, model, Android version.
- `DeviceChanges`: Uses `DeviceMonitor` from the ADB client library to receive real-time connect/disconnect notifications. Emits `DeviceDiscoveryEvent` (Added, Removed, Changed).
- Classifies `ConnectionType` based on serial format: serials containing `:` (colon with port) are `Wireless`; others are `USB`.

## Implementation: AdbDeviceController
- `CaptureScreenshotAsync()`: Executes `adb -s {serial} exec-out screencap -p` and captures PNG bytes. Falls back to `adb shell screencap -p /sdcard/screen.png` + `adb pull` on older devices.
- `TapAsync()`: Executes `adb -s {serial} shell input tap {x} {y}`.
- `SwipeAsync()`: Executes `adb -s {serial} shell input swipe {x1} {y1} {x2} {y2} {durationMs}`.
- All methods accept `CancellationToken` and enforce timeout (`SETTING-LLM-003` equivalent for ADB).

## Device Lifecycle State Machine

```
[Discovered] ──connect──→ [Connecting] ──success──→ [Connected] ──verify──→ [Verifying] ──ok──→ [Ready]
     ↑                         │                        │                      │
     │                     fail/timeout             disconnect              fail
     │                         ↓                        ↓                      ↓
     └──────────────── [Unreachable]              [Disconnected]          [Error]
                            │                        │
                        retry/scan                reconnect
                            ↓                        ↓
                       [Connecting]              [Connecting]

Special states:
[Unauthorized] — Device visible but user hasn't approved ADB prompt.
[Offline] — ADB reports device as offline.
```

## Wireless-Specific Handling
### Pairing (Android 11+)
1. User provides IP:Port + 6-digit pairing code from device's Developer Options → Wireless Debugging.
2. App calls `adb pair <ip>:<port> <code>` via CLI fallback.
3. On success, the device appears in discovery as `Wireless / Connecting`.

### Connection
1. `adb connect <ip>:<port>` via `IDeviceConnectionManager.ConnectWirelessAsync()`.
2. Port defaults to 5555 for legacy `tcpip` mode, or the dynamic port shown in Wireless Debugging for Android 11+.

### Reconnect Strategy
- On disconnect detection, if `SETTING-AUT-003` (auto-reconnect) is enabled:
  1. Wait 2 seconds.
  2. Attempt `adb connect` to last known endpoint.
  3. Retry up to 5 times with exponential backoff (2s, 4s, 8s, 16s, 32s).
  4. If all fail, transition to `Unreachable`, notify user.
- IP change detection: If the device reconnects with a different IP (e.g., after DHCP renewal), the serial changes. The app matches by device model + Android ID when possible.

### Endpoint Persistence
- Successfully paired wireless endpoints are stored in `settings.json` under `Device.SavedWirelessEndpoints`.
- On app startup, the system attempts to connect to saved endpoints.

## Coordinate Translation
The automation engine works with **relative coordinates** (0.0-1.0) from the LLM. The ADB layer converts:
```
absoluteX = (int)(relativeX * screenWidth)
absoluteY = (int)(relativeY * screenHeight)
```
This conversion happens inside `AdbDeviceController`, keeping the rest of the system resolution-independent.

## Multi-Device Support
- All ADB commands use `-s {serial}` to target a specific device.
- The `DeviceService` in Application layer maintains the `SelectedDeviceSerial` state.
- Only one device is active for automation at a time (domain invariant), but discovery shows all.

## Testing Approach
- `FakeDeviceDiscovery` and `FakeDeviceController` in tests: return predefined device lists and screenshot images.
- Integration tests: require a real ADB server running, optionally with an emulator.
- Reconnect tests: simulate disconnect by removing device from fake discovery, then re-adding.
