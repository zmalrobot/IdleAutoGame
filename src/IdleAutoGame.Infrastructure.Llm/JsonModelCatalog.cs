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
            FileSize = 1_107_409_952L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/Qwen3VL-2B-Instruct-Q4_K_M.gguf",
            Checksum = "089d75c52f4b7ffc56ba998ffc50aae89fcafc755f9e7208aacca281dca6c2ae",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-Qwen3VL-2B-Instruct-F16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-2B-Instruct-F16.gguf",
            MmprojChecksum = "c3d5afbef5287953acd57b4043d2269456e5761a4eaccb3b71b062996970aea5",
            MmprojFileSize = 819_394_848L,
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
            FileSize = 2_497_281_664L,
            RamRequirementMb = 5632,
            RecommendedRamRange = "6 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/Qwen3VL-4B-Instruct-Q4_K_M.gguf",
            Checksum = "66358cb18bb6b3b1b6675aa412c7a88ef01d228f481184d13668e5201c730a0a",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-Qwen3VL-4B-Instruct-F16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-4B-Instruct-F16.gguf",
            MmprojChecksum = "256f3a43bd4205ffef48d6b92715e1e70b5b0e9aef06522584967513a9985331",
            MmprojFileSize = 836_180_256L,
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
            FileSize = 1_112_242_368L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/ggml-org/SmolVLM-Instruct-GGUF/resolve/main/SmolVLM-Instruct-Q4_K_M.gguf",
            Checksum = "dc80966bd84789de64115f07888939c03abb1714d431c477dfb405517a554af5",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-SmolVLM-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/SmolVLM-Instruct-GGUF/resolve/main/mmproj-SmolVLM-Instruct-f16.gguf",
            MmprojChecksum = "670c0a23196cce1be845e704775c68a4f757e6aea6898314201b46529070bdb7",
            MmprojFileSize = 872_301_824L,
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
            FileSize = 3_106_738_272L,
            RamRequirementMb = 4608,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF/resolve/main/gemma-4-E2B-it-Q4_K_M.gguf",
            Checksum = "740185b21d22ceb83a11c3aa62ad5842ef32c70f6096d756bbee85a1e4ec34b8",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-gemma-4-e2b-it-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF/resolve/main/mmproj-F16.gguf",
            MmprojChecksum = "140be8d7849741f88c50757d529b84373ee8e27052cc2236855b537f4a8215fa",
            MmprojFileSize = 985_654_080L,
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
            FileSize = 5_027_784_800L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/main/Qwen3VL-8B-Instruct-Q4_K_M.gguf",
            Checksum = "67d1659bfe71b89d50b45a4ad1a9e5b997e5bb16ce5da66a6a6167abd569e9e2",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-Qwen3VL-8B-Instruct-F16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-8B-Instruct-F16.gguf",
            MmprojChecksum = "ca524100ebf825c9a870db1c580d03879e0da0ab2541697e2458e64891cf9d38",
            MmprojFileSize = 1_159_029_824L,
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
            FileSize = 4_681_132_224L,
            RamRequirementMb = 10240,
            RecommendedRamRange = "10 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/ggml-org/InternVL3-8B-Instruct-GGUF/resolve/main/InternVL3-8B-Instruct-Q4_K_M.gguf",
            Checksum = "645b5db5754711f90adcc85dd99217a66d1306c97c2a2d3ae69071e3dea7a42e",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-InternVL3-8B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/InternVL3-8B-Instruct-GGUF/resolve/main/mmproj-InternVL3-8B-Instruct-f16.gguf",
            MmprojChecksum = "7ad5f0c3e49de0f63a50ebf22c0bb16961175154ca32604d3fb083602d9d5d51",
            MmprojFileSize = 666_002_720L,
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
            FileSize = 4_977_171_584L,
            RamRequirementMb = 8192,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-E4B-it-GGUF/resolve/main/gemma-4-E4B-it-Q4_K_M.gguf",
            Checksum = "85a896a047553e842f25297ee5b031d64ff30147d9c4af17b1e4b394cd1fab87",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-gemma-4-e4b-it-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-E4B-it-GGUF/resolve/main/mmproj-F16.gguf",
            MmprojChecksum = "ddf46c21d7078e95338cfc22306b19b276a29a5ad089023449dd54d4b6170a51",
            MmprojFileSize = 990_372_672L,
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
            FileSize = 18_556_687_168L,
            RamRequirementMb = 20480,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-30B-A3B-Instruct-GGUF/resolve/main/Qwen3VL-30B-A3B-Instruct-Q4_K_M.gguf",
            Checksum = "87bb374d849f80ebdfabb304189fac9e0bd35a0f74506e6a59c51b206cbe863b",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-Qwen3VL-30B-A3B-Instruct-F16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-30B-A3B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-30B-A3B-Instruct-F16.gguf",
            MmprojChecksum = "cae72cf123cc9e08d553cd5a5055d6d3cf0f82652aa41c3e4aa424cda9a26f7f",
            MmprojFileSize = 1_083_499_584L,
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
            FileSize = 19_762_150_432L,
            RamRequirementMb = 24576,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-32B-Instruct-GGUF/resolve/main/Qwen3VL-32B-Instruct-Q4_K_M.gguf",
            Checksum = "5cf0136e721d6294718ec71fd8c93b17ab5dd4e2714d6079e83fa46571ad94c8",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-Qwen3VL-32B-Instruct-F16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen3-VL-32B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-32B-Instruct-F16.gguf",
            MmprojChecksum = "8617824839df91f84b4840ad5084dcf50a1403a435a1f4cfc4d8c84ce6cac2fc",
            MmprojFileSize = 1_196_795_040L,
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
            FileSize = 16_947_541_728L,
            RamRequirementMb = 20480,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/unsloth/gemma-4-26B-A4B-it-GGUF/resolve/main/gemma-4-26B-A4B-it-UD-Q4_K_M.gguf",
            Checksum = "f2c28b3dc4776931ac6f879e11f203dec637ea0f14267a86ec8f6165f63f293f",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-gemma-4-26b-a4b-it-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/unsloth/gemma-4-26B-A4B-it-GGUF/resolve/main/mmproj-F16.gguf",
            MmprojChecksum = "418a6d8723067cd712235facbbc5cba6c8fbbd413fc1292d2aace5a027d5a42f",
            MmprojFileSize = 1_193_058_784L,
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
