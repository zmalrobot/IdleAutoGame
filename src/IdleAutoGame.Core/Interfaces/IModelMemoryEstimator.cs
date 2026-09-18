using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Service calculating VRAM requirements, KV cache budgets, and recommended GPU layer offloading.
/// </summary>
public interface IModelMemoryEstimator
{
    /// <summary>
    /// Computes a structured memory and offload estimation for the given model file, context size, and target GPU.
    /// </summary>
    /// <param name="modelPath">Path to the GGUF model weights file.</param>
    /// <param name="device">Target Vulkan graphics device (or null if CPU only).</param>
    /// <param name="settings">Configured GPU settings.</param>
    /// <param name="contextSize">Context window length in tokens.</param>
    /// <param name="mmprojPath">Optional multimodal projector file path (for vision models).</param>
    /// <returns>A detailed <see cref="ModelMemoryEstimate"/> outlining required VRAM and recommended offload layer count.</returns>
    ModelMemoryEstimate Estimate(
        string modelPath,
        VulkanGpuDevice? device,
        GpuSettings settings,
        int contextSize,
        string? mmprojPath = null);
}

