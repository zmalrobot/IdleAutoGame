using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Predefined hardware VRAM profile tuning offload behavior, buffer reserves, and context budgets.
/// </summary>
public sealed record GpuMemoryProfile
{
    /// <summary>
    /// Gets the unique key of the profile ("auto", "6gb", "8gb", "16gb", "32gb", "custom").
    /// </summary>
    public string Id { get; init; } = "8gb";

    /// <summary>
    /// Gets the user-facing display name of the profile.
    /// </summary>
    public string DisplayName { get; init; } = "8 GB VRAM";

    /// <summary>
    /// Gets the minimum VRAM threshold in megabytes for this profile to apply.
    /// </summary>
    public long MinVramMb { get; init; }

    /// <summary>
    /// Gets the maximum VRAM threshold in megabytes for this profile to apply.
    /// </summary>
    public long MaxVramMb { get; init; }

    /// <summary>
    /// Gets the reserved VRAM in megabytes for operating system display compositor, desktop UI, and video output.
    /// </summary>
    public int ReservedVramMb { get; init; } = 1024;

    /// <summary>
    /// Gets the maximum recommended context window size in tokens under this profile.
    /// </summary>
    public int MaxRecommendedContextTokens { get; init; } = 8192;

    /// <summary>
    /// Gets the default offload mode recommended for this tier.
    /// </summary>
    public GpuOffloadMode RecommendedOffloadMode { get; init; } = GpuOffloadMode.Auto;

    /// <summary>
    /// Gets the target percentage of layers to budget for offload when partial offloading is required (0.0 - 1.0).
    /// </summary>
    public double DefaultLayerBudgetRatio { get; init; } = 0.80;

    /// <summary>
    /// Gets descriptive recommendations for this profile.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    // ── Predefined Static Profiles ──────────────────────────────────────────────

    /// <summary>
    /// 6 GB VRAM profile (conservative layer budgeting and context limits for 7B/8B models).
    /// </summary>
    public static GpuMemoryProfile Profile6Gb => new()
    {
        Id = "6gb",
        DisplayName = "6 GB VRAM Profile",
        MinVramMb = 5120,
        MaxVramMb = 7168,
        ReservedVramMb = 768,
        MaxRecommendedContextTokens = 4096,
        RecommendedOffloadMode = GpuOffloadMode.Auto,
        DefaultLayerBudgetRatio = 0.65,
        Description = "Consigliato per GPU con 6 GB VRAM. Predilige offload parziale (16-22 layer) per modelli 7B-8B Q4."
    };

    /// <summary>
    /// 8 GB VRAM profile (optimal for AMD Radeon RX 480/580, RTX 2060/3070 8GB).
    /// </summary>
    public static GpuMemoryProfile Profile8Gb => new()
    {
        Id = "8gb",
        DisplayName = "8 GB VRAM Profile",
        MinVramMb = 7168,
        MaxVramMb = 14336,
        ReservedVramMb = 1024,
        MaxRecommendedContextTokens = 8192,
        RecommendedOffloadMode = GpuOffloadMode.Auto,
        DefaultLayerBudgetRatio = 0.85,
        Description = "Consigliato per GPU con 8 GB VRAM. Supporta full GPU per modelli 7B Q4 con context 4096-8192 o partial offload (24-28 layer) per Q5/Q6."
    };

    /// <summary>
    /// 16 GB VRAM profile (supports full offload of 7B-8B models with large context, and partial offload for 14B).
    /// </summary>
    public static GpuMemoryProfile Profile16Gb => new()
    {
        Id = "16gb",
        DisplayName = "16 GB VRAM Profile",
        MinVramMb = 14336,
        MaxVramMb = 28672,
        ReservedVramMb = 1536,
        MaxRecommendedContextTokens = 16384,
        RecommendedOffloadMode = GpuOffloadMode.Full,
        DefaultLayerBudgetRatio = 1.0,
        Description = "Consigliato per GPU con 16 GB VRAM. Offload completo per modelli 7B-8B con ampi contesti e parziale per modelli 14B."
    };

    /// <summary>
    /// 32 GB VRAM profile (large-scale models up to 32B full offload).
    /// </summary>
    public static GpuMemoryProfile Profile32Gb => new()
    {
        Id = "32gb",
        DisplayName = "32 GB VRAM Profile",
        MinVramMb = 28672,
        MaxVramMb = long.MaxValue,
        ReservedVramMb = 2048,
        MaxRecommendedContextTokens = 32768,
        RecommendedOffloadMode = GpuOffloadMode.Full,
        DefaultLayerBudgetRatio = 1.0,
        Description = "Consigliato per GPU di fascia alta (32+ GB VRAM). Offload completo per modelli fino a 32B parametri."
    };

    /// <summary>
    /// All standard versioned profiles.
    /// </summary>
    public static IReadOnlyList<GpuMemoryProfile> AllProfiles => new[]
    {
        Profile6Gb,
        Profile8Gb,
        Profile16Gb,
        Profile32Gb
    };
}

