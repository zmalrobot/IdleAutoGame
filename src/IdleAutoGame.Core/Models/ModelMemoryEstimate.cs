using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Structured memory and layer offload calculation produced by the memory estimator for a model and target GPU.
/// </summary>
public sealed record ModelMemoryEstimate
{
    /// <summary>
    /// Gets a value indicating whether the model weights and complete KV cache fit comfortably in available VRAM.
    /// </summary>
    public bool FitsFullGpu { get; init; }

    /// <summary>
    /// Gets a value indicating whether a useful subset of model layers (at least 4) can be offloaded to GPU.
    /// </summary>
    public bool FitsPartialGpu { get; init; }

    /// <summary>
    /// Gets the recommended number of layers to offload to the GPU (0 to TotalModelLayers).
    /// </summary>
    public int RecommendedGpuLayers { get; init; }

    /// <summary>
    /// Gets the total number of transformer/attention blocks in the model architecture.
    /// </summary>
    public int TotalModelLayers { get; init; } = 33;

    /// <summary>
    /// Gets the estimated VRAM footprint in bytes for the recommended configuration (weights + KV cache + overhead).
    /// </summary>
    public long EstimatedGpuMemoryBytes { get; init; }

    /// <summary>
    /// Gets the estimated VRAM footprint in megabytes.
    /// </summary>
    public long EstimatedGpuMemoryMb => EstimatedGpuMemoryBytes / (1024 * 1024);

    /// <summary>
    /// Gets the estimated total memory footprint (RAM + VRAM) across the system in bytes.
    /// </summary>
    public long EstimatedTotalMemoryBytes { get; init; }

    /// <summary>
    /// Gets the safety margin of free VRAM remaining in bytes after the estimated allocation.
    /// </summary>
    public long MemoryMarginBytes { get; init; }

    /// <summary>
    /// Gets the resulting offload mode recommended by the evaluation.
    /// </summary>
    public GpuOffloadMode RecommendedOffloadMode { get; init; } = GpuOffloadMode.Auto;

    /// <summary>
    /// Gets a human-readable diagnostic explanation of the offload decision and constraints.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets an offload estimate representing pure CPU execution.
    /// </summary>
    public static ModelMemoryEstimate CreateCpuOnly(string reason, int totalLayers = 33) => new()
    {
        FitsFullGpu = false,
        FitsPartialGpu = false,
        RecommendedGpuLayers = 0,
        TotalModelLayers = totalLayers,
        RecommendedOffloadMode = GpuOffloadMode.CpuOnly,
        Reason = reason
    };
}

