using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Metadata describing an AI model, its hardware prerequisites, quality, and capabilities.
/// </summary>
public sealed record ModelProfile
{
    /// <summary>
    /// Default OS/desktop RAM overhead headroom in megabytes (1.5 GB).
    /// Prevents Linux OOM killer crashes on systems running close to hardware limits.
    /// </summary>
    public const int DefaultSystemRamHeadroomMb = 1536;

    /// <summary>
    /// Gets the unique model identifier (e.g., 'llava-v1.6-vicuna-7b-q4').
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the friendly model name for UI display.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the provider family (e.g., 'llama.cpp', 'openai', 'ollama').
    /// </summary>
    public string Provider { get; init; } = "llama.cpp";

    /// <summary>
    /// Gets the minimum host RAM in megabytes required to execute the model safely.
    /// </summary>
    public int RequiredRamMb { get; init; }

    /// <summary>
    /// Gets the minimum GPU VRAM in megabytes required for hardware acceleration, if applicable.
    /// </summary>
    public int? RequiredVramMb { get; init; }

    /// <summary>
    /// Gets the expected quality tier of the model.
    /// </summary>
    public QualityTier QualityTier { get; init; } = QualityTier.Medium;

    /// <summary>
    /// Gets the expected inference speed tier.
    /// </summary>
    public SpeedTier SpeedTier { get; init; } = SpeedTier.Medium;

    /// <summary>
    /// Gets a value indicating whether the model supports image/vision inputs.
    /// </summary>
    public bool SupportsVision { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the model supports JSON schema constrained outputs.
    /// </summary>
    public bool SupportsJsonSchema { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the model executes locally on the machine.
    /// </summary>
    public bool IsLocal { get; init; } = true;

    /// <summary>
    /// Gets the optional local file path or model file name (GGUF).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the human-readable description and recommendation rationale.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Checks whether this model can run on the detected hardware, reserving safety headroom for the OS.
    /// </summary>
    /// <param name="hardware">Detected hardware specifications.</param>
    /// <param name="reason">Explanation when incompatible.</param>
    /// <param name="systemRamHeadroomMb">Safety headroom in MB for the OS and desktop environment.</param>
    /// <returns>True if compatible; otherwise false.</returns>
    public bool IsCompatibleWith(HardwareInfo hardware, out string reason, int systemRamHeadroomMb = DefaultSystemRamHeadroomMb)
    {
        long usableRamMb = Math.Max(0, hardware.TotalRamMb - systemRamHeadroomMb);

        if (hardware.TotalRamMb > 0 && RequiredRamMb > usableRamMb)
        {
            reason = $"Requires {RequiredRamMb / 1024.0:F1} GB RAM. Usable RAM after OS reserve is {usableRamMb / 1024.0:F1} GB (Total: {hardware.TotalRamMb / 1024.0:F1} GB).";
            return false;
        }

        if (RequiredVramMb.HasValue && hardware.VramMb.HasValue && RequiredVramMb.Value > hardware.VramMb.Value)
        {
            reason = $"Requires {RequiredVramMb.Value / 1024.0:F1} GB VRAM (GPU has {hardware.VramMb.Value / 1024.0:F1} GB)";
            return false;
        }

        reason = "Compatible";
        return true;
    }
}
