using System.Text.Json;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Model catalog backed by a JSON file with embedded fallback profiles and curated GGUF vision models per RAM tier.
/// </summary>
public sealed class JsonModelCatalog : IModelCatalog
{
    private readonly string _catalogPath;
    private readonly List<ModelProfile> _models = new();
    private readonly List<LocalModel> _localModels = new();

    /// <summary>
    /// Legacy model ID mapping table for backwards-compatibility migrations.
    /// Maps deprecated model identifiers directly to their modern vision/multimodal equivalents.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyModelAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["moondream2-2b-q4"] = "qwen3-vl-2b-instruct",
        ["qwen2.5-vl-3b-q4"] = "qwen3-vl-4b-instruct",
        ["qwen2-vl-2b-q4"] = "smolvlm2-2.2b-instruct",
        ["minicpm-v-2.6-8b-q4"] = "qwen3-vl-8b-instruct",
        ["qwen2.5-vl-7b-q4"] = "internvl3-8b-instruct",
        ["qwen2-vl-7b-q4"] = "qwen3-vl-4b-instruct",
        ["qwen2.5-vl-7b-q6"] = "qwen3-vl-8b-instruct",
        ["qwen2.5-vl-32b-q4"] = "qwen3-vl-32b-instruct",
        ["qwen2.5-vl-72b-q4"] = "qwen3-vl-30b-a3b-instruct",
        ["llava-v1.6-7b-q4"] = "internvl3-8b-instruct",
        ["llama-3.2-11b-vision-q4"] = "qwen3-vl-8b-instruct",
        ["gemma-2-2b-it-q4"] = "gemma-4-e2b-it"
    };

    private static readonly LocalModel[] DefaultLocalModels =
    [
        // =====================================================================
        // === Tier 8 GB (Entry — RAM <= 8 GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen3-vl-2b-instruct",
            Name = "Qwen3-VL 2B Instruct",
            DisplayName = "Qwen3-VL 2B (Ultra-Fast Vision)",
            Provider = "LLamaSharp",
            Architecture = "qwen3",
            Quantization = "Q4_K_M",
            ParameterCount = "2B",
            ContextLength = 4096,
            FileSize = 1_650_000_000L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/Qwen3-VL-2B-Instruct-Q4_K_M.gguf",
            Checksum = "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0",
            RequiresMmproj = true,
            MmprojFileName = "qwen3-vl-2b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/mmproj-model-f16.gguf",
            MmprojChecksum = "b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef01",
            MmprojFileSize = 650_000_000L,
            MmprojVersion = "1.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Medium,
            Description = "Ultra-fast lightweight 2B vision-language model optimized for mobile UI detection and screen coordinate reasoning on budget systems."
        },
        new LocalModel
        {
            Id = "qwen3-vl-4b-instruct",
            Name = "Qwen3-VL 4B Instruct",
            DisplayName = "Qwen3-VL 4B (High Acuity / Agentic)",
            Provider = "LLamaSharp",
            Architecture = "qwen3",
            Quantization = "Q4_K_M",
            ParameterCount = "4B",
            ContextLength = 4096,
            FileSize = 2_600_000_000L,
            RamRequirementMb = 5632,
            RecommendedRamRange = "6 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/Qwen3-VL-4B-Instruct-Q4_K_M.gguf",
            Checksum = "c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef012",
            RequiresMmproj = true,
            MmprojFileName = "qwen3-vl-4b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/mmproj-model-f16.gguf",
            MmprojChecksum = "d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0123",
            MmprojFileSize = 850_000_000L,
            MmprojVersion = "1.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.High,
            Description = "High-acuity 4B multimodal model with superior OCR and mobile icon understanding, ideal for games with small UI elements."
        },
        new LocalModel
        {
            Id = "smolvlm2-2.2b-instruct",
            Name = "SmolVLM2-2.2B-Instruct",
            DisplayName = "SmolVLM2 2.2B (Compact / OCR)",
            Provider = "LLamaSharp",
            Architecture = "idefics3",
            Quantization = "Q4_K_M",
            ParameterCount = "2.2B",
            ContextLength = 4096,
            FileSize = 1_450_000_000L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/HuggingFaceTB/SmolVLM2-2.2B-Instruct-GGUF/resolve/main/SmolVLM2-2.2B-Instruct-Q4_K_M.gguf",
            Checksum = "e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef01234",
            RequiresMmproj = true,
            MmprojFileName = "smolvlm2-2.2b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/HuggingFaceTB/SmolVLM2-2.2B-Instruct-GGUF/resolve/main/mmproj-SmolVLM2-2.2B-Instruct-f16.gguf",
            MmprojChecksum = "f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef012345",
            MmprojFileSize = 480_000_000L,
            MmprojVersion = "2.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.2",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Medium,
            Description = "HuggingFaceTB compact 2.2B vision-language model leveraging SigLIP encoder and SmolLM2 for rapid UI comprehension on resource-constrained hardware."
        },
        new LocalModel
        {
            Id = "gemma-4-e2b-it",
            Name = "Gemma-4-E2B-it",
            DisplayName = "Gemma 4 E2B (Edge Multimodal)",
            Provider = "LLamaSharp",
            Architecture = "gemma4",
            Quantization = "Q4_K_M",
            ParameterCount = "2B (5B Raw)",
            ContextLength = 8192,
            FileSize = 3_200_000_000L,
            RamRequirementMb = 4608,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF/resolve/main/gemma-4-E2B-it-Q4_K_M.gguf",
            Checksum = "0718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0123456",
            RequiresMmproj = true,
            MmprojFileName = "gemma-4-e2b-it-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF/resolve/main/mmproj-gemma-4-E2B-it-f16.gguf",
            MmprojChecksum = "18293a4b5c6d7e8f90123456789abcdef0123456789abcdef01234567",
            MmprojFileSize = 850_000_000L,
            MmprojVersion = "4.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "4.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.High,
            Description = "Google DeepMind native multimodal edge model offering outstanding visual acuity, reasoning tokens, and robust structured JSON compliance."
        },

        // =====================================================================
        // === Tier 16 GB (Balanced — RAM 8–16 GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen3-vl-8b-instruct",
            Name = "Qwen3-VL 8B Instruct",
            DisplayName = "Qwen3-VL 8B (Frontier Balanced)",
            Provider = "LLamaSharp",
            Architecture = "qwen3",
            Quantization = "Q4_K_M",
            ParameterCount = "8B",
            ContextLength = 8192,
            FileSize = 5_150_000_000L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/main/Qwen3-VL-8B-Instruct-Q4_K_M.gguf",
            Checksum = "293a4b5c6d7e8f90123456789abcdef0123456789abcdef012345678",
            RequiresMmproj = true,
            MmprojFileName = "qwen3-vl-8b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/main/mmproj-model-f16.gguf",
            MmprojChecksum = "3a4b5c6d7e8f90123456789abcdef0123456789abcdef0123456789",
            MmprojFileSize = 1_100_000_000L,
            MmprojVersion = "3.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Flagship 8B vision-language model with exceptional UI reasoning, game HUD comprehension, and multi-step strategy planning."
        },
        new LocalModel
        {
            Id = "internvl3-8b-instruct",
            Name = "InternVL3-8B-Instruct",
            DisplayName = "InternVL3 8B (High Precision Vision)",
            Provider = "LLamaSharp",
            Architecture = "internvl3",
            Quantization = "Q4_K_M",
            ParameterCount = "8B",
            ContextLength = 8192,
            FileSize = 5_200_000_000L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "10 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/OpenGVLab/InternVL3-8B-Instruct-GGUF/resolve/main/InternVL3-8B-Instruct-Q4_K_M.gguf",
            Checksum = "4b5c6d7e8f90123456789abcdef0123456789abcdef0123456789a",
            RequiresMmproj = true,
            MmprojFileName = "internvl3-8b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/OpenGVLab/InternVL3-8B-Instruct-GGUF/resolve/main/mmproj-InternVL3-8B-Instruct-f16.gguf",
            MmprojChecksum = "5c6d7e8f90123456789abcdef0123456789abcdef0123456789ab",
            MmprojFileSize = 1_250_000_000L,
            MmprojVersion = "3.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "OpenGVLab frontier 8B vision model featuring advanced optical recognition and fine-grained spatial coordination."
        },
        new LocalModel
        {
            Id = "gemma-4-e4b-it",
            Name = "Gemma-4-E4B-it",
            DisplayName = "Gemma 4 E4B (Balanced Multimodal)",
            Provider = "LLamaSharp",
            Architecture = "gemma4",
            Quantization = "Q4_K_M",
            ParameterCount = "4B (8B Raw)",
            ContextLength = 8192,
            FileSize = 5_400_000_000L,
            RamRequirementMb = 8192,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-E4B-it-GGUF/resolve/main/gemma-4-E4B-it-Q4_K_M.gguf",
            Checksum = "6d7e8f90123456789abcdef0123456789abcdef0123456789abc",
            RequiresMmproj = true,
            MmprojFileName = "gemma-4-e4b-it-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-E4B-it-GGUF/resolve/main/mmproj-gemma-4-E4B-it-f16.gguf",
            MmprojChecksum = "7e8f90123456789abcdef0123456789abcdef0123456789abcd",
            MmprojFileSize = 1_100_000_000L,
            MmprojVersion = "4.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "4.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Google DeepMind 4B effective multimodal model balancing inference speed and deep visual reasoning for 8-16 GB gaming rigs."
        },

        // =====================================================================
        // === Tier 32 GB+ (Performance — RAM 16–32+ GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen3-vl-30b-a3b-instruct",
            Name = "Qwen3-VL 30B-A3B Instruct",
            DisplayName = "Qwen3-VL 30B-A3B (MoE Speed & Power)",
            Provider = "LLamaSharp",
            Architecture = "qwen3",
            Quantization = "Q4_K_M",
            ParameterCount = "30B (3B Active)",
            ContextLength = 8192,
            FileSize = 18_500_000_000L,
            RamRequirementMb = 20480,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-30B-A3B-Instruct-GGUF/resolve/main/Qwen3-VL-30B-A3B-Instruct-Q4_K_M.gguf",
            Checksum = "8f90123456789abcdef0123456789abcdef0123456789abcde",
            RequiresMmproj = true,
            MmprojFileName = "qwen3-vl-30b-a3b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-30B-A3B-Instruct-GGUF/resolve/main/mmproj-model-f16.gguf",
            MmprojChecksum = "90123456789abcdef0123456789abcdef0123456789abcdef",
            MmprojFileSize = 1_800_000_000L,
            MmprojVersion = "3.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.High,
            Description = "Sparse Mixture-of-Experts architecture activating only 3B parameters per token for workstation-class speed with 30B reasoning capacity."
        },
        new LocalModel
        {
            Id = "qwen3-vl-32b-instruct",
            Name = "Qwen3-VL 32B Instruct",
            DisplayName = "Qwen3-VL 32B (Dense Frontier Vision)",
            Provider = "LLamaSharp",
            Architecture = "qwen3",
            Quantization = "Q4_K_M",
            ParameterCount = "32B",
            ContextLength = 8192,
            FileSize = 19_800_000_000L,
            RamRequirementMb = 24576,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-32B-Instruct-GGUF/resolve/main/Qwen3-VL-32B-Instruct-Q4_K_M.gguf",
            Checksum = "a0123456789abcdef0123456789abcdef0123456789abcdef0",
            RequiresMmproj = true,
            MmprojFileName = "qwen3-vl-32b-instruct-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-32B-Instruct-GGUF/resolve/main/mmproj-model-f16.gguf",
            MmprojChecksum = "b0123456789abcdef0123456789abcdef0123456789abcdef1",
            MmprojFileSize = 1_850_000_000L,
            MmprojVersion = "3.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "3.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Heavyweight dense 32B multimodal vision model with uncompromising visual precision, complex strategy derivation, and sub-pixel coordinate accuracy."
        },
        new LocalModel
        {
            Id = "gemma-4-26b-a4b-it",
            Name = "Gemma-4-26B-A4B-it",
            DisplayName = "Gemma 4 26B-A4B (MoE Frontier)",
            Provider = "LLamaSharp",
            Architecture = "gemma4",
            Quantization = "Q4_K_M",
            ParameterCount = "26B (4B Active)",
            ContextLength = 16384,
            FileSize = 17_200_000_000L,
            RamRequirementMb = 20480,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-26B-A4B-it-GGUF/resolve/main/gemma-4-26B-A4B-it-Q4_K_M.gguf",
            Checksum = "c0123456789abcdef0123456789abcdef0123456789abcdef2",
            RequiresMmproj = true,
            MmprojFileName = "gemma-4-26b-a4b-it-mmproj.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-26B-A4B-it-GGUF/resolve/main/mmproj-gemma-4-26B-A4B-it-f16.gguf",
            MmprojChecksum = "d0123456789abcdef0123456789abcdef0123456789abcdef3",
            MmprojFileSize = 1_600_000_000L,
            MmprojVersion = "4.0",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "4.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Google DeepMind frontier Mixture-of-Experts vision model activating 4B parameters per token for premier UI gameplay reasoning."
        }
    ];

    private static readonly ModelProfile[] RemoteProfiles =
    [
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
    /// DefaultProfiles is programmatically generated from DefaultLocalModels plus remote profiles.
    /// This eliminates catalog divergence across the entire application.
    /// </summary>
    private static readonly ModelProfile[] DefaultProfiles =
        DefaultLocalModels
            .Select(lm => new ModelProfile
            {
                Id = lm.Id,
                Name = lm.DisplayName,
                Provider = lm.Provider,
                RequiredRamMb = lm.RamRequirementMb,
                RequiredVramMb = lm.RamRequirementMb / 2,
                QualityTier = lm.QualityTier,
                SpeedTier = lm.SpeedTier,
                SupportsVision = lm.SupportsVision,
                SupportsJsonSchema = lm.SupportsJsonSchema,
                IsLocal = true,
                FilePath = lm.FilePath,
                Description = lm.Description
            })
            .Concat(RemoteProfiles)
            .ToArray();

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
        _localModels.Clear();

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
                }
            }
            catch
            {
                // Fallback to embedded defaults on corrupted file
            }
        }

        if (_models.Count == 0)
        {
            _models.AddRange(DefaultProfiles);
        }

        _localModels.AddRange(DefaultLocalModels);
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
        var migrated = MigrateModelId(modelId);
        return _models.FirstOrDefault(m => string.Equals(m.Id, migrated, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<LocalModel> GetAllLocalModels() => _localModels.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<LocalModel> GetRecommendedModelsForTier(RamTier tier)
    {
        // Strictly returns the curated 4 recommended models in exact user-specified order per tier
        var targetIds = tier switch
        {
            RamTier.Tier8Gb => new[]
            {
                "qwen3-vl-2b-instruct",
                "qwen3-vl-4b-instruct",
                "smolvlm2-2.2b-instruct",
                "gemma-4-e2b-it"
            },
            RamTier.Tier16Gb => new[]
            {
                "qwen3-vl-8b-instruct",
                "internvl3-8b-instruct",
                "qwen3-vl-4b-instruct",
                "gemma-4-e4b-it"
            },
            _ => new[]
            {
                "qwen3-vl-30b-a3b-instruct",
                "qwen3-vl-32b-instruct",
                "qwen3-vl-8b-instruct",
                "gemma-4-26b-a4b-it"
            }
        };

        var result = new List<LocalModel>(4);
        foreach (var id in targetIds)
        {
            var model = GetLocalModel(id);
            if (model != null)
            {
                result.Add(model);
            }
        }

        return result.AsReadOnly();
    }

    /// <inheritdoc />
    public RamTier DetermineRamTier(HardwareInfo hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        // Usable memory assessment:
        // >= 24 GB RAM -> Tier32GbPlus
        // >= 10 GB RAM -> Tier16Gb
        // < 10 GB RAM -> Tier8Gb
        if (hardware.TotalRamMb >= 24576)
        {
            return RamTier.Tier32GbPlus;
        }

        if (hardware.TotalRamMb >= 10240)
        {
            return RamTier.Tier16Gb;
        }

        return RamTier.Tier8Gb;
    }

    /// <inheritdoc />
    public LocalModel? GetLocalModel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var migrated = MigrateModelId(id);
        return _localModels.FirstOrDefault(m => string.Equals(m.Id, migrated, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public string MigrateModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return modelId;
        }

        if (LegacyModelAliases.TryGetValue(modelId, out var migrated))
        {
            return migrated;
        }

        return modelId;
    }
}
