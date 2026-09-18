namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Lifecycle and health state of the GPU acceleration system.
/// </summary>
public enum GpuUsageState
{
    /// <summary>
    /// GPU acceleration has been explicitly disabled by user settings.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// No compatible GPU or Vulkan runtime is available on the system.
    /// </summary>
    Unavailable = 1,

    /// <summary>
    /// GPU backend is initializing or probing device capabilities.
    /// </summary>
    Initializing = 2,

    /// <summary>
    /// GPU is initialized, compatible, and ready for model loading.
    /// </summary>
    Ready = 3,

    /// <summary>
    /// Model weights and tensors are currently being offloaded to GPU memory.
    /// </summary>
    Loading = 4,

    /// <summary>
    /// GPU is actively performing full inference.
    /// </summary>
    Active = 5,

    /// <summary>
    /// GPU is actively performing partial offload inference in tandem with CPU.
    /// </summary>
    Partial = 6,

    /// <summary>
    /// GPU initialization or inference encountered a fatal error and has fallen back.
    /// </summary>
    Failed = 7
}

