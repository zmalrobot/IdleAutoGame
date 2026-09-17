using System.Text.Json;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Model catalog backed by a JSON file with embedded fallback profiles and curated GGUF models per RAM tier.
/// </summary>
public sealed class JsonModelCatalog : IModelCatalog
{
    private readonly string _catalogPath;
    private readonly List<ModelProfile> _models = new();
    private readonly List<LocalModel> _localModels = new();

    private static readonly LocalModel[] DefaultLocalModels =
    [
        // === Tier 8 GB (Entry) ===
        new LocalModel
        {
            Id = "moondream2-2b-q4",
            Name = "Moondream2 2B Instruct",
            DisplayName = "Moondream2 2B (Fast / Budget)",
            Provider = "LLamaSharp",
            Architecture = "moondream",
            Quantization = "F16",
            ParameterCount = "2B",
            ContextLength = 2048,
            FileSize = 2_839_534_976L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/moondream/moondream2-gguf/resolve/main/moondream2-text-model-f16.gguf",
            Checksum = "4e17e9107fb8781629b3c8ce177de57ffeae90fe14adcf7b99f0eef025889696",
            Version = "2.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Low,
            Description = "Ultralight 2B vision model with low memory footprint and high inference speed for budget systems with 4-8 GB RAM."
        },
        new LocalModel
        {
            Id = "qwen2.5-vl-3b-q4",
            Name = "Qwen2.5-VL 3B Instruct",
            DisplayName = "Qwen2.5-VL 3B (Fast / Vision)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "3B",
            ContextLength = 4096,
            FileSize = 1_929_901_408L,
            RamRequirementMb = 5120,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/unsloth/Qwen2.5-VL-3B-Instruct-GGUF/resolve/main/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf",
            Checksum = "c47e8c1f6fb3e8cff6ec58909baff16dbeffb64a5bb3b746b96e05e6334c129f",
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Medium,
            Description = "Compact 3B vision-language model with sharp OCR and mobile UI reasoning for entry-level systems."
        },
        new LocalModel
        {
            Id = "qwen2-vl-2b-q4",
            Name = "Qwen2-VL 2B Instruct",
            DisplayName = "Qwen2-VL 2B (High Acuity)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "2B",
            ContextLength = 4096,
            FileSize = 986_047_232L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/bartowski/Qwen2-VL-2B-Instruct-GGUF/resolve/main/Qwen2-VL-2B-Instruct-Q4_K_M.gguf",
            Checksum = "4ef095263343fc1237e8ca879790bb262bcf209f082e0a9bfce219b7ece55e8b",
            Version = "2.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Medium,
            Description = "High spatial acuity for mobile UI screen parsing and icon detection on entry-level hardware."
        },

        // === Tier 16 GB (Balanced) ===
        new LocalModel
        {
            Id = "minicpm-v-2.6-8b-q4",
            Name = "MiniCPM-V 2.6 8B",
            DisplayName = "MiniCPM-V 2.6 8B (Advanced Vision)",
            Provider = "LLamaSharp",
            Architecture = "minicpm",
            Quantization = "Q4_K_M",
            ParameterCount = "8B",
            ContextLength = 4096,
            FileSize = 4_681_089_344L,
            RamRequirementMb = 9216,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/openbmb/MiniCPM-V-2_6-gguf/resolve/main/ggml-model-Q4_K_M.gguf",
            Checksum = "3a4078d53b46f22989adbf998ce5a3fd090b6541f112d7e936eb4204a04100b1",
            Version = "2.6",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Superior optical character recognition and small icon comprehension on mobile screens."
        },
        new LocalModel
        {
            Id = "qwen2.5-vl-7b-q4",
            Name = "Qwen2.5-VL 7B Instruct",
            DisplayName = "Qwen2.5-VL 7B (State of the Art)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "7B",
            ContextLength = 8192,
            FileSize = 4_683_072_384L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "10 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/unsloth/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf",
            Checksum = "d16776dcd9a28d42758c2958ed3a752aabf20a305252cd64ff2be72b4a78c503",
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "State of the art multimodal model with outstanding UI coordinate detection and reasoning."
        },
        new LocalModel
        {
            Id = "qwen2-vl-7b-q4",
            Name = "Qwen2-VL 7B Instruct",
            DisplayName = "Qwen2-VL 7B (Strategic Reasoning)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "7B",
            ContextLength = 4096,
            FileSize = 4_683_072_672L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "12 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/bartowski/Qwen2-VL-7B-Instruct-GGUF/resolve/main/qwen2-vl-7b-instruct-q4_k_m.gguf",
            Checksum = "30f199c2192fce1db0fbbbd484c7b2aa69ccce883890853f9807e1c837405a80",
            Version = "2.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Excellent multi-step game state reasoning and highly reliable JSON schema output formatting."
        },

        // === Tier 32 GB+ (Performance) ===
        new LocalModel
        {
            Id = "qwen2.5-vl-7b-q6",
            Name = "Qwen2.5-VL 7B Q6_K",
            DisplayName = "Qwen2.5-VL 7B Q6 (High Precision)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q6_K",
            ParameterCount = "7B",
            ContextLength = 8192,
            FileSize = 6_254_197_632L,
            RamRequirementMb = 16384,
            RecommendedRamRange = "16 - 32 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/unsloth/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/Qwen2.5-VL-7B-Instruct-Q6_K.gguf",
            Checksum = "15f3ccbef1e7020939d8c32501d66a777df46b1b3ace9ddec37b5d2df102a89f",
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "High-precision Q6 quantization for maximum perceptual fidelity and complex UI logic on 16-32 GB systems."
        },
        new LocalModel
        {
            Id = "qwen2.5-vl-32b-q4",
            Name = "Qwen2.5-VL 32B Instruct",
            DisplayName = "Qwen2.5-VL 32B (Frontier Vision)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "32B",
            ContextLength = 8192,
            FileSize = 19_851_335_200L,
            RamRequirementMb = 24576,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/unsloth/Qwen2.5-VL-32B-Instruct-GGUF/resolve/main/Qwen2.5-VL-32B-Instruct-Q4_K_M.gguf",
            Checksum = "292b39eab9089d5195182d6d1e35e907181bb0d4d2a0b5e73ab4e7938ecefdf6",
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Frontier 32B multimodal vision model with state of the art visual reasoning and UI planning."
        },
        new LocalModel
        {
            Id = "qwen2.5-vl-72b-q4",
            Name = "Qwen2.5-VL 72B Instruct",
            DisplayName = "Qwen2.5-VL 72B (Heavyweight Frontier)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "72B",
            ContextLength = 8192,
            FileSize = 47_415_714_208L,
            RamRequirementMb = 49152,
            RecommendedRamRange = "48+ GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/unsloth/Qwen2.5-VL-72B-Instruct-GGUF/resolve/main/Qwen2.5-VL-72B-Instruct-Q4_K_M.gguf",
            Checksum = "ec42ea2c536aec5510af2ddd3125e81d494833de070255e933867f65dae8ec21",
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Slow,
            QualityTier = QualityTier.High,
            Description = "Heavyweight 72B frontier model for maximum accuracy on high-end workstations and servers with 48+ GB RAM."
        }
    ];

    private static readonly ModelProfile[] DefaultProfiles =
    [
        new ModelProfile
        {
            Id = "moondream2-2b-q4",
            Name = "Moondream2 2B (Fast / Low RAM)",
            Provider = "LLamaSharp",
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
            Provider = "LLamaSharp",
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
            Provider = "LLamaSharp",
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
        return _models.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<LocalModel> GetAllLocalModels() => _localModels.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<LocalModel> GetRecommendedModelsForTier(RamTier tier)
    {
        return _localModels.Where(m => m.RamTier == tier).Take(3).ToList().AsReadOnly();
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
        return _localModels.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
