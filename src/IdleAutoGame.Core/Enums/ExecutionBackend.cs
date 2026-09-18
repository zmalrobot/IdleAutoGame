namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Actual execution backend utilized by the active LLM inference engine.
/// </summary>
public enum ExecutionBackend
{
    /// <summary>
    /// Unknown or uninitialized execution backend.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Executing entirely on the host CPU.
    /// </summary>
    Cpu = 1,

    /// <summary>
    /// Executing fully accelerated on the GPU via Vulkan.
    /// </summary>
    VulkanGpu = 2,

    /// <summary>
    /// Executing in hybrid mode (partially on GPU via Vulkan and remainder on CPU).
    /// </summary>
    VulkanGpuPartial = 3,

    /// <summary>
    /// Remote HTTP endpoint or other execution provider.
    /// </summary>
    Other = 4
}

