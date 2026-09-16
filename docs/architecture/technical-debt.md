---
title: Technical Debt Registry
status: draft
version: 1.0
date: 2026-09-16
---

# Technical Debt Registry

Known technical debt items, tracked for future resolution.

## TD-001: Manual llama-server Lifecycle
**Status**: Accepted (MVP)
**Description**: The user must manually start/stop `llama-server` and load models. The app does not manage the llama.cpp process lifecycle.
**Impact**: Higher barrier to entry for non-technical users.
**Resolution**: Post-MVP — implement `LlamaServerManager` to start/stop the process and switch models.
**Traceability**: Open Decision #3 from Phase 1.

## TD-002: Manual ADB Wireless Pairing
**Status**: Accepted (MVP)
**Description**: ADB Wireless pairing (Android 11+) requires CLI fallback (`adb pair`). The ADB client library doesn't expose pairing natively.
**Impact**: Slightly inconsistent UX between USB (automatic) and Wireless (semi-manual).
**Resolution**: Post-MVP — evaluate direct TCP implementation of ADB pairing protocol, or wrap CLI in a seamless UX flow.
**Traceability**: `DEVICE-WIRELESS-001`, ADR-003.

## TD-003: No Model Download Manager
**Status**: Accepted (MVP)
**Description**: Users must download GGUF model files manually. The model catalog describes models but doesn't download them.
**Impact**: Users need external tools to obtain models.
**Resolution**: Post-MVP — add download progress UI and model file management.
**Traceability**: Open Decision #3 from Phase 1, `LLM-002`.

## TD-004: Single-Threaded Screenshot Encoding
**Status**: Accepted (MVP)
**Description**: Screenshot PNG-to-base64 encoding is synchronous in the cycle. For very high-resolution devices, this may add 100-200ms.
**Impact**: Minor latency addition.
**Resolution**: If profiling shows this is significant, pipeline the encoding (start encoding while preparing the prompt).

## TD-005: No AppImage Packaging
**Status**: Accepted (MVP)
**Description**: MVP ships as a self-contained folder. No single-file AppImage distribution.
**Impact**: Slightly harder distribution.
**Resolution**: Post-MVP — add AppImage build script.
**Traceability**: ADR (deployment).

