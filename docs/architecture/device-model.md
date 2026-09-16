---
title: Device Model
status: draft
version: 1.0
date: 2026-09-16
---

# Device Model

## DeviceInfo Value Object
Immutable snapshot of a discovered Android device. Created by the discovery layer and consumed by the rest of the application.

### Properties
| Property | Type | Description | Source |
|---|---|---|---|
| Serial | string | ADB serial identifier | `adb devices` |
| DisplayName | string | "Manufacturer Model" | Derived |
| Manufacturer | string | e.g., "Google" | `ro.product.manufacturer` |
| Model | string | e.g., "Pixel 8" | `ro.product.model` |
| AndroidVersion | string | e.g., "14" | `ro.build.version.release` |
| ScreenResolution | Resolution | Width × Height | `wm size` |
| Density | int | DPI | `wm density` |
| State | DeviceState | Current lifecycle state | ADB + app logic |
| ConnectionType | ConnectionType | USB / Wireless / Unknown | Serial format analysis |
| NetworkEndpoint | string? | IP:Port for wireless | Serial parsing |
| Capabilities | DeviceCapabilities | Feature flags | Probed |
| LastSeen | DateTimeOffset | Last time device was seen | App timestamp |

### DeviceCapabilities
Flags indicating what the device supports:
- `ScreenCapture: bool` — Can capture screenshots.
- `InputInjection: bool` — Can inject touch events.
- `ShellAccess: bool` — Can run shell commands.

Defaults to all `true` for authorized devices. Set to `false` if a capability probe fails.

### Connection Type Detection
```
If serial contains ":" (e.g., "192.168.1.5:5555") → Wireless
Else (e.g., "ABCD1234") → USB
Fallback → Unknown
```

### Identification Strategy for Multi-Device
Problem: Two Pixel 8 devices could have the same display name.
Solution:
1. Primary identifier is always the ADB `serial` (guaranteed unique by ADB server).
2. `DisplayName` is for UI convenience only.
3. The settings reference devices by `serial`, not display name.
4. The UI shows serial as a subtitle when multiple devices share the same model name.

### Unified Device List (Presentation)
The Presentation layer receives `IReadOnlyList<DeviceInfo>`. It renders a single list, sorting by:
1. State (Ready first, then Connected, then others).
2. Connection type (no preference — mixed).
3. Display name alphabetically.

No separate USB/Wireless lists. No conditional UI branches.
