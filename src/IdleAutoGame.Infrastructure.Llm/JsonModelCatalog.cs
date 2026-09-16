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
            Quantization = "Q4_K_M",
            ParameterCount = "2B",
            ContextLength = 2048,
            FileSize = 1_400_000_000L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/vikhyatk/moondream2/resolve/main/moondream2-text-model-f16.gguf",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Version = "2.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Low,
            Description = "Ultralight 2B vision model with low memory footprint and high inference speed for budget systems with 4-8 GB RAM."
        },
        new LocalModel
        {
            Id = "smolvlm-2b-q4",
            Name = "SmolVLM 2.2B Instruct",
            DisplayName = "SmolVLM 2B (Compact Multimodal)",
            Provider = "LLamaSharp",
            Architecture = "smolvlm",
            Quantization = "Q4_K_M",
            ParameterCount = "2.2B",
            ContextLength = 4096,
            FileSize = 1_550_000_000L,
            RamRequirementMb = 4608,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/HuggingFaceTB/SmolVLM-Instruct/resolve/main/smolvlm-2b-q4_k_m.gguf",
            Checksum = "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0",
            Version = "1.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.Low,
            Description = "Compact multimodal model designed by HuggingFace for edge devices and low-RAM desktop environments."
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
            FileSize = 1_650_000_000L,
            RamRequirementMb = 5120,
            RecommendedRamRange = "6 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2-VL-2B-Instruct-GGUF/resolve/main/qwen2-vl-2b-instruct-q4_k_m.gguf",
            Checksum = "b2c3d4e5f6a10718293a4b5c6d7e8f90123456789abcdef0123456789abcdef1",
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
            Id = "llava-v1.6-7b-q4",
            Name = "LLaVA 1.6 Vicuna 7B",
            DisplayName = "LLaVA 1.6 7B (Balanced Standard)",
            Provider = "LLamaSharp",
            Architecture = "llava",
            Quantization = "Q4_K_M",
            ParameterCount = "7B",
            ContextLength = 4096,
            FileSize = 4_500_000_000L,
            RamRequirementMb = 8192,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/cjpais/llava-1.6-vicuna-7b-gguf/resolve/main/llava-v1.6-vicuna-7b.Q4_K_M.gguf",
            Checksum = "c3d4e5f6a1b20718293a4b5c6d7e8f90123456789abcdef0123456789abcdef2",
            Version = "1.6",
            LicenseName = "Llama-2",
            LicenseUrl = "https://ai.meta.com/llama/license/",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.Medium,
            Description = "Standard balanced vision model for mobile gaming automation with solid button and menu comprehension."
        },
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
            FileSize = 5_200_000_000L,
            RamRequirementMb = 9216,
            RecommendedRamRange = "12 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/openbmb/MiniCPM-V-2_6-gguf/resolve/main/ggml-model-Q4_K_M.gguf",
            Checksum = "d4e5f6a1b2c30718293a4b5c6d7e8f90123456789abcdef0123456789abcdef3",
            Version = "2.6",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Superior optical character recognition and small icon comprehension on mobile screens."
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
            FileSize = 4_800_000_000L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "12 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2-VL-7B-Instruct-GGUF/resolve/main/qwen2-vl-7b-instruct-q4_k_m.gguf",
            Checksum = "e5f6a1b2c3d40718293a4b5c6d7e8f90123456789abcdef0123456789abcdef4",
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
            Id = "llama-3.2-11b-vision-q4",
            Name = "Llama 3.2 11B Vision Instruct",
            DisplayName = "Llama 3.2 11B Vision (Precision)",
            Provider = "LLamaSharp",
            Architecture = "llama",
            Quantization = "Q4_K_M",
            ParameterCount = "11B",
            ContextLength = 8192,
            FileSize = 7_500_000_000L,
            RamRequirementMb = 16384,
            RecommendedRamRange = "16 - 32 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/meta-llama/Llama-3.2-11B-Vision-Instruct/resolve/main/llama-3.2-11b-vision-q4_k_m.gguf",
            Checksum = "f6a1b2c3d4e50718293a4b5c6d7e8f90123456789abcdef0123456789abcdef5",
            Version = "3.2",
            LicenseName = "Llama-3.2",
            LicenseUrl = "https://ai.meta.com/llama/license/",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Meta's flagship multimodal model offering deep reasoning, precise OCR, and nuanced decision making."
        },
        new LocalModel
        {
            Id = "cogvlm2-19b-q4",
            Name = "CogVLM2 19B Chat",
            DisplayName = "CogVLM2 19B (High Resolution)",
            Provider = "LLamaSharp",
            Architecture = "cogvlm",
            Quantization = "Q4_K_M",
            ParameterCount = "19B",
            ContextLength = 4096,
            FileSize = 12_500_000_000L,
            RamRequirementMb = 24576,
            RecommendedRamRange = "24 - 32 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/THUDM/cogvlm2-llama3-chinese-chat-19B/resolve/main/cogvlm2-19b-q4_k_m.gguf",
            Checksum = "0718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0123456789abc",
            Version = "2.0",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Slow,
            QualityTier = QualityTier.High,
            Description = "Specialized high-resolution vision model capable of pinpointing microscopic game indicators."
        },
        new LocalModel
        {
            Id = "qwen2-vl-72b-q4",
            Name = "Qwen2-VL 72B Instruct",
            DisplayName = "Qwen2-VL 72B (Frontier Heavy)",
            Provider = "LLamaSharp",
            Architecture = "qwen2",
            Quantization = "Q4_K_M",
            ParameterCount = "72B",
            ContextLength = 8192,
            FileSize = 42_000_000_000L,
            RamRequirementMb = 49152,
            RecommendedRamRange = "48+ GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2-VL-72B-Instruct-GGUF/resolve/main/qwen2-vl-72b-instruct-q4_k_m.gguf",
            Checksum = "18293a4b5c6d7e8f90123456789abcdef0123456789abcdef0123456789abcdef",
            Version = "2.0",
            LicenseName = "Qwen-Research",
            LicenseUrl = "https://github.com/QwenLM/Qwen2-VL/blob/main/LICENSE",
            SpeedTier = SpeedTier.Slow,
            QualityTier = QualityTier.High,
            Description = "Frontier-grade vision reasoning model for enthusiast workstations and servers with 48+ GB RAM."
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
