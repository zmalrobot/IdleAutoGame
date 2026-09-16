using System.Text.Json;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Model catalog backed by a JSON file with embedded fallback profiles.
/// </summary>
public sealed class JsonModelCatalog : IModelCatalog
{
    private readonly string _catalogPath;
    private readonly List<ModelProfile> _models = new();

    private static readonly ModelProfile[] DefaultProfiles =
    [
        new ModelProfile
        {
            Id = "moondream2-2b-q4",
            Name = "Moondream2 2B (Fast / Low RAM)",
            Provider = "llama.cpp",
            RequiredRamMb = 4096,
            RequiredVramMb = 2048,
            QualityTier = QualityTier.Low,
            SpeedTier = SpeedTier.Fast,
            SupportsVision = true,
            SupportsJsonSchema = true,
            IsLocal = true,
            Description = "Extremely lightweight vision model for budget systems with 4-8 GB RAM."
        },
        new ModelProfile
        {
            Id = "llava-v1.6-7b-q4",
            Name = "LLaVA 1.6 7B Q4_K_M (Balanced)",
            Provider = "llama.cpp",
            RequiredRamMb = 8192,
            RequiredVramMb = 4096,
            QualityTier = QualityTier.Medium,
            SpeedTier = SpeedTier.Medium,
            SupportsVision = true,
            SupportsJsonSchema = true,
            IsLocal = true,
            Description = "Standard recommended vision model for gaming automation with 8-16 GB RAM."
        },
        new ModelProfile
        {
            Id = "llama-3.2-11b-vision-q4",
            Name = "Llama 3.2 11B Vision Q4 (High Precision)",
            Provider = "llama.cpp",
            RequiredRamMb = 16384,
            RequiredVramMb = 8192,
            QualityTier = QualityTier.High,
            SpeedTier = SpeedTier.Slow,
            SupportsVision = true,
            SupportsJsonSchema = true,
            IsLocal = true,
            Description = "Advanced reasoning and UI comprehension for systems with 16+ GB RAM."
        },
        new ModelProfile
        {
            Id = "openai-gpt-4o-mini",
            Name = "OpenAI GPT-4o Mini (Cloud)",
            Provider = "openai-compatible",
            RequiredRamMb = 0,
            RequiredVramMb = null,
            QualityTier = QualityTier.High,
            SpeedTier = SpeedTier.Fast,
            SupportsVision = true,
            SupportsJsonSchema = true,
            IsLocal = false,
            Description = "Remote cloud inference via OpenAI-compatible endpoint. No local GPU required."
        }
    ];

    /// <summary>
    /// Initializes a new instance of <see cref="JsonModelCatalog"/>.
    /// </summary>
    /// <param name="catalogPath">Optional path to custom models.json file.</param>
    public JsonModelCatalog(string? catalogPath = null)
    {
        _catalogPath = catalogPath ?? Path.Combine(AppContext.BaseDirectory, "models.json");
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        _models.Clear();

        if (File.Exists(_catalogPath))
        {
            try
            {
                var json = File.ReadAllText(_catalogPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var loaded = JsonSerializer.Deserialize<List<ModelProfile>>(json, options);
                if (loaded != null && loaded.Count > 0)
                {
                    _models.AddRange(loaded);
                    return;
                }
            }
            catch
            {
                // Fallback to embedded defaults on corrupted file
            }
        }

        // Use embedded default profiles
        _models.AddRange(DefaultProfiles);
    }

    /// <inheritdoc />
    public IReadOnlyList<ModelProfile> GetAllModels() => _models.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<ModelProfile> GetCompatibleModels(HardwareInfo hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        return _models.Where(m => m.IsCompatibleWith(hardware, out _)).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public ModelProfile? GetModel(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        return _models.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }
}

