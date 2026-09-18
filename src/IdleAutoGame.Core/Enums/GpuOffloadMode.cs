namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Mode of offloading model layers and KV cache onto the GPU.
/// </summary>
public enum GpuOffloadMode
{
    /// <summary>
    /// Automatically determines whether full or partial offload is feasible based on model size and available VRAM.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Attempts to offload 100% of model layers and context KV cache to GPU VRAM.
    /// </summary>
    Full = 1,

    /// <summary>
    /// Offloads an optimal subset of layers to GPU VRAM while keeping the rest on host CPU.
    /// </summary>
    Partial = 2,

    /// <summary>
    /// Forces inference to run strictly on the CPU, disabling GPU acceleration.
    /// </summary>
    CpuOnly = 3
}

