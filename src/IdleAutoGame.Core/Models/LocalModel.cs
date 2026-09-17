using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Domain entity representing a local GGUF model manageable by the application.
/// </summary>
public sealed record LocalModel
{
    /// <summary>
    /// Gets the unique identifier for the model (e.g., 'moondream2-2b-q4').
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the technical model name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the user-friendly display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the runtime provider family (defaults to 'LLamaSharp').
    /// </summary>
    public string Provider { get; init; } = "LLamaSharp";

    /// <summary>
    /// Gets the model neural architecture (e.g., 'llama', 'moondream', 'qwen2').
    /// </summary>
    public string Architecture { get; init; } = "llama";

    /// <summary>
    /// Gets the quantization format (e.g., 'Q4_K_M', 'Q5_K_M').
    /// </summary>
    public string Quantization { get; init; } = "Q4_K_M";

    /// <summary>
    /// Gets the parameter count label (e.g., '2B', '7B', '11B').
    /// </summary>
    public string ParameterCount { get; init; } = "7B";

    /// <summary>
    /// Gets the default context window token length.
    /// </summary>
    public int ContextLength { get; init; } = 4096;

    /// <summary>
    /// Gets the absolute or relative file path on disk when installed.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets the model file size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Gets the verified download URL for the GGUF model file.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Gets the SHA-256 integrity checksum for verification.
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Gets a value indicating whether this vision model requires a separate multimodal projector (mmproj) GGUF.
    /// </summary>
    public bool RequiresMmproj { get; init; } = false;

    /// <summary>
    /// Gets the optional custom file name for the mmproj file on disk.
    /// </summary>
    public string? MmprojFileName { get; init; }

    /// <summary>
    /// Gets the verified download URL for the mmproj projector GGUF file.
    /// </summary>
    public string? MmprojDownloadUrl { get; init; }

    /// <summary>
    /// Gets the SHA-256 integrity checksum for the mmproj projector file.
    /// </summary>
    public string? MmprojChecksum { get; init; }

    /// <summary>
    /// Gets the mmproj projector file size in bytes.
    /// </summary>
    public long MmprojFileSize { get; init; }

    /// <summary>
    /// Gets the absolute or relative file path on disk to the mmproj projector when installed.
    /// </summary>
    public string? MmprojFilePath { get; set; }

    /// <summary>
    /// Gets the version tag for the mmproj projector asset.
    /// </summary>
    public string? MmprojVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the model supports image/vision inputs.
    /// </summary>
    public bool SupportsVision { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the model supports JSON schema constrained output decoding.
    /// </summary>
    public bool SupportsJsonSchema { get; init; } = true;

    /// <summary>
    /// Gets or sets the current installation/operational status of the model.
    /// </summary>
    public ModelStatus Status { get; set; } = ModelStatus.NotInstalled;

    /// <summary>
    /// Gets the minimum host RAM in megabytes required to execute the model safely.
    /// </summary>
    public int RamRequirementMb { get; init; }

    /// <summary>
    /// Gets the human-readable recommended RAM range (e.g. '8 - 16 GB').
    /// </summary>
    public string RecommendedRamRange { get; init; } = string.Empty;

    /// <summary>
    /// Gets the RAM tier classification.
    /// </summary>
    public RamTier RamTier { get; init; } = RamTier.Tier16Gb;

    /// <summary>
    /// Gets the model release version.
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Gets the model open-source license name (e.g. 'Apache-2.0', 'MIT').
    /// </summary>
    public string? LicenseName { get; init; }

    /// <summary>
    /// Gets the link to the license text.
    /// </summary>
    public string? LicenseUrl { get; init; }

    /// <summary>
    /// Gets the expected inference speed tier.
    /// </summary>
    public SpeedTier SpeedTier { get; init; } = SpeedTier.Medium;

    /// <summary>
    /// Gets the output reasoning quality tier.
    /// </summary>
    public QualityTier QualityTier { get; init; } = QualityTier.Medium;

    /// <summary>
    /// Gets the descriptive explanation and recommendation rationale.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the model was installed locally.
    /// </summary>
    public DateTimeOffset? InstalledAt { get; set; }

    /// <summary>
    /// Checks whether this model is compatible with the given hardware.
    /// </summary>
    public bool IsCompatibleWith(HardwareInfo hardware, out string reason, int systemRamHeadroomMb = ModelProfile.DefaultSystemRamHeadroomMb)
    {
        long usableRamMb = Math.Max(0, hardware.TotalRamMb - systemRamHeadroomMb);

        if (hardware.TotalRamMb > 0 && RamRequirementMb > usableRamMb)
        {
            reason = $"Requires {RamRequirementMb / 1024.0:F1} GB RAM. Usable RAM after OS reserve is {usableRamMb / 1024.0:F1} GB (Total: {hardware.TotalRamMb / 1024.0:F1} GB).";
            return false;
        }

        reason = "Compatible";
        return true;
    }
}

