---
title: "ADR-003: Unified ADB Abstraction Layer"
status: accepted
date: 2026-09-16
---

# ADR-003: Unified ADB Abstraction Layer

## Status
Accepted

## Date
2026-09-16

## Context
The application must support both USB and Wireless ADB connections transparently. Product requirements DEVICE-DISCOVERY-001 and DEVICE-003 demand a unified device list and identical workflow regardless of connection type. The application must also support multiple simultaneous devices.

## Alternatives Considered
1. **Direct `adb` CLI calls scattered throughout**: Simple but creates tight coupling. USB/Wireless branching would infect every caller.
2. **ADB client library (e.g., SharpAdbClient/Madb)**: Provides TCP socket communication with the ADB server. Avoids process spawning for every command.
3. **Hybrid**: Use an ADB client library for device tracking and command execution, with fallback to CLI for pairing and advanced operations.

## Decision
Use **AdvancedSharpAdbClient (Madb fork)** as the primary ADB communication layer, wrapped behind our own `IDeviceManager` and `IDeviceController` interfaces defined in Core.

Reasoning:
- It communicates directly with the ADB server via TCP socket (port 5037), avoiding per-command process spawning overhead.
- Supports device tracking (monitor connect/disconnect events) natively.
- Provides screenshot via `framebuffer:` or `screencap` command routing.
- Input events (tap, swipe) via `input` shell commands.
- We wrap it behind our interfaces so it can be replaced later (e.g., by scrcpy socket for lower latency).

For ADB Wireless pairing (Android 11+ developer wireless debugging), we fall back to CLI (`adb pair <ip>:<port> <code>`) since the library doesn't expose pairing.

## Consequences
- The Core project defines `IDeviceManager`, `IDeviceController`, `IDeviceDiscovery` with no ADB knowledge.
- Infrastructure.Adb implements these using AdvancedSharpAdbClient.
- USB and Wireless devices are represented by the same `DeviceInfo` domain model.
- Connection type is metadata (an enum), not a behavioral branch.
- Future: `Infrastructure.Scrcpy` could provide a faster `IDeviceController` implementation.

## Traceability
- `DEVICE-USB-001`, `DEVICE-WIRELESS-001` → Both connection types.
- `DEVICE-DISCOVERY-001` → Unified list.
- `DEVICE-003` → Transport-agnostic workflow.
