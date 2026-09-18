using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm.Gpu;

/// <summary>
/// Estimates model weights, KV cache, and multimodal VRAM requirements to determine
/// whether a model can be executed fully on GPU, partially offloaded, or requires CPU fallback.
/// </summary>
public sealed class ModelMemoryEstimator : IModelMemoryEstimator
{
    private const long DefaultRuntimeOverheadBytes = 450 * 1024 * 1024; // ~450 MB runtime & scratch buffer

    /// <inheritdoc />
    public ModelMemoryEstimate Estimate(
        string modelPath,
        VulkanGpuDevice? device,
        GpuSettings settings,
        int contextSize,
        string? mmprojPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        int totalLayers = EstimateTotalLayers(modelPath);

        // 1. Check if GPU usage is disabled
        if (!settings.UseGpu || settings.OffloadMode == GpuOffloadMode.CpuOnly)
        {
            return ModelMemoryEstimate.CreateCpuOnly("GPU disabilitata nelle impostazioni (CPU only).", totalLayers);
        }

        // 2. Check if a valid Vulkan device is present
        if (device == null || !device.SupportsVulkan)
        {
            return ModelMemoryEstimate.CreateCpuOnly("Nessuna GPU compatibile Vulkan disponibile per l'offload.", totalLayers);
        }

        // 3. Obtain weights file size
        long modelSizeBytes = 5_000_000_000; // ~5 GB default estimate
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                modelSizeBytes = new FileInfo(modelPath).Length;
            }
            catch
            {
                // Fallback to default estimate
            }
        }

        // 4. Multimodal projector size
        long mmprojSizeBytes = 0;
        if (!string.IsNullOrWhiteSpace(mmprojPath) && File.Exists(mmprojPath))
        {
            try
            {
                mmprojSizeBytes = new FileInfo(mmprojPath).Length;
            }
            catch
            {
                // Fallback
            }
        }

        // 5. KV Cache estimation based on context length and layer count
        // Standard GQA formula: ~200 KB per context token across all layers for 7B-8B architectures
        long kvCacheBytes = (long)contextSize * 200 * 1024;

        // 6. Usable VRAM computation
        long reserveBytes = (long)settings.GpuMemoryReserveMb * 1024 * 1024;
        long minFreeBytes = (long)settings.GpuMinFreeMemoryMb * 1024 * 1024;
        long totalVramBytes = device.DedicatedVideoMemoryBytes > 0
            ? device.DedicatedVideoMemoryBytes
            : device.TotalUsableMemoryBytes;

        long availableVramBytes = Math.Max(0, totalVramBytes - reserveBytes);

        if (availableVramBytes < minFreeBytes)
        {
            return ModelMemoryEstimate.CreateCpuOnly(
                $"VRAM disponibile ({availableVramBytes / (1024 * 1024)} MB) inferiore alla riserva minima di sicurezza ({settings.GpuMinFreeMemoryMb} MB).",
                totalLayers);
        }

        // 7. Full GPU Offload Check
        long fullGpuRequiredBytes = modelSizeBytes + kvCacheBytes + mmprojSizeBytes + DefaultRuntimeOverheadBytes;

        if (fullGpuRequiredBytes <= availableVramBytes)
        {
            long margin = availableVramBytes - fullGpuRequiredBytes;
            return new ModelMemoryEstimate
            {
                FitsFullGpu = true,
                FitsPartialGpu = true,
                RecommendedGpuLayers = totalLayers,
                TotalModelLayers = totalLayers,
                EstimatedGpuMemoryBytes = fullGpuRequiredBytes,
                EstimatedTotalMemoryBytes = fullGpuRequiredBytes,
                MemoryMarginBytes = margin,
                RecommendedOffloadMode = GpuOffloadMode.Full,
                Reason = $"Il modello entra completamente in VRAM ({fullGpuRequiredBytes / (1024 * 1024):F0} MB stimati su {totalVramBytes / (1024 * 1024):F0} MB disponibili, margine {margin / (1024 * 1024):F0} MB)."
            };
        }

        // 8. Partial GPU Offload Check
        long bytesPerLayer = (modelSizeBytes + kvCacheBytes) / Math.Max(1, totalLayers);
        long budgetForLayers = Math.Max(0, availableVramBytes - DefaultRuntimeOverheadBytes - mmprojSizeBytes);

        int maxLayersThatFit = (int)(budgetForLayers / Math.Max(1, bytesPerLayer));
        int recommendedLayers = Math.Clamp(maxLayersThatFit, 0, totalLayers);

        // Explicit layer count override if user set a positive value
        if (settings.GpuLayerCount > 0)
        {
            recommendedLayers = Math.Clamp(settings.GpuLayerCount, 0, totalLayers);
        }

        if (recommendedLayers >= 4) // Minimum useful offload threshold
        {
            long partialGpuBytes = (recommendedLayers * bytesPerLayer) + DefaultRuntimeOverheadBytes + mmprojSizeBytes;
            long margin = availableVramBytes - partialGpuBytes;

            return new ModelMemoryEstimate
            {
                FitsFullGpu = false,
                FitsPartialGpu = true,
                RecommendedGpuLayers = recommendedLayers,
                TotalModelLayers = totalLayers,
                EstimatedGpuMemoryBytes = partialGpuBytes,
                EstimatedTotalMemoryBytes = modelSizeBytes + kvCacheBytes + mmprojSizeBytes + DefaultRuntimeOverheadBytes,
                MemoryMarginBytes = margin,
                RecommendedOffloadMode = GpuOffloadMode.Partial,
                Reason = $"Offload parziale ottimale: {recommendedLayers} di {totalLayers} layer su GPU ({partialGpuBytes / (1024 * 1024):F0} MB VRAM stimati), restanti {totalLayers - recommendedLayers} layer su CPU."
            };
        }

        // 9. VRAM insufficient even for minimal partial offload
        return ModelMemoryEstimate.CreateCpuOnly(
            $"VRAM insufficiente ({availableVramBytes / (1024 * 1024)} MB disponibili per offload) per sostenere il numero minimo di layer. Esecuzione su CPU.",
            totalLayers);
    }

    private static int EstimateTotalLayers(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return 33;

        string name = Path.GetFileNameWithoutExtension(modelPath).ToLowerInvariant();

        if (name.Contains("32b") || name.Contains("30b")) return 64;
        if (name.Contains("14b") || name.Contains("13b")) return 40;
        if (name.Contains("7b") || name.Contains("8b")) return 33;
        if (name.Contains("3b") || name.Contains("4b")) return 28;
        if (name.Contains("1b") || name.Contains("2b") || name.Contains("0.5b")) return 24;

        return 33; // Default for 7B-8B class models
    }
}

