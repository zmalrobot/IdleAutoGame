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
        // 8 GB Tier Legacy Aliases
        ["moondream2-2b-q4"] = "qwen2.5-vl-3b-instruct",
        ["qwen2-vl-2b-q4"] = "smolvlm2-2.2b-instruct",
        ["qwen2.5-vl-3b-q4"] = "qwen2.5-vl-3b-instruct",
        ["qwen3-vl-2b-instruct"] = "qwen2.5-vl-3b-instruct",
        ["qwen3-vl-4b-instruct"] = "qwen2.5-vl-3b-instruct",
        ["gemma-2-2b-it-q4"] = "qwen2.5-vl-3b-instruct",
        ["gemma-4-e2b-it"] = "qwen2.5-vl-3b-instruct",

        // 16 GB Tier Legacy Aliases
        ["qwen2-vl-7b-q4"] = "qwen2.5-vl-7b-instruct",
        ["qwen2.5-vl-7b-q4"] = "qwen2.5-vl-7b-instruct",
        ["qwen2.5-vl-7b-q6"] = "qwen2.5-vl-7b-instruct",
        ["qwen3-vl-8b-instruct"] = "qwen2.5-vl-7b-instruct",
        ["internvl3-8b-instruct"] = "internvl2.5-8b-instruct",
        ["minicpm-v-2.6-8b-q4"] = "qwen2.5-vl-7b-instruct",
        ["llava-v1.6-7b-q4"] = "internvl2.5-8b-instruct",
        ["llama-3.2-11b-vision-q4"] = "qwen2.5-vl-7b-instruct",
        ["gemma-4-e4b-it"] = "qwen2.5-vl-7b-instruct",

        // 32 GB+ Tier Legacy Aliases
        ["qwen2.5-vl-32b-q4"] = "internvl2.5-26b-instruct",
        ["qwen2.5-vl-72b-q4"] = "qwen2.5-vl-72b-instruct",
        ["qwen3-vl-30b-a3b-instruct"] = "qwen2.5-vl-72b-instruct",
        ["qwen3-vl-32b-instruct"] = "internvl2.5-26b-instruct",
        ["gemma-4-26b-a4b-it"] = "internvl2.5-26b-instruct"
    };

    private static readonly LocalModel[] DefaultLocalModels =
    [
        // =====================================================================
        // === Tier 8 GB (Entry — RAM <= 8 GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen2.5-vl-3b-instruct",
            Name = "Qwen2.5-VL 3B Instruct",
            DisplayName = "Qwen2.5-VL 3B (Top Mobile UI / Fast)",
            Provider = "LLamaSharp",
            Architecture = "qwen2vl",
            Quantization = "Q5_K_M",
            Quantization = "Q4_K_M",
            ParameterCount = "3B",
            ContextLength = 8192,
            FileSize = 2_760_000_000L,
            FileSize = 1_929_901_056L,
            RamRequirementMb = 4096,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-3B-Instruct-GGUF/resolve/main/qwen2.5-vl-3b-instruct-q5_k_m.gguf",
            Checksum = "c5b20757d529b84373ee8e27052cc2236855b537f4a8215facb412c7a88ef01d",
            DownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-3B-Instruct-GGUF/resolve/main/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf",
            Checksum = "d02fe9b69ad8cadbbd228e387667af66612c44bed29ffc8eb1e7caf9ac486c12",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-qwen2.5-vl-3b-instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-3B-Instruct-GGUF/resolve/main/mmproj-qwen2.5-vl-3b-instruct-f16.gguf",
            MmprojChecksum = "a1b5afbef5287953acd57b4043d2269456e5761a4eaccb3b71b062996970aea5",
            MmprojFileSize = 880_000_000L,
            MmprojFileName = "mmproj-Qwen2.5-VL-3B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-3B-Instruct-GGUF/resolve/main/mmproj-Qwen2.5-VL-3B-Instruct-f16.gguf",
            MmprojChecksum = "b9160fe9d814d1fadf68395677468534778b39ac33c2e7561b7b218626e60d5e",
            MmprojFileSize = 1_338_428_128L,
            MmprojVersion = "2.5",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.High,
            Description = "Leader mondiale sotto i 4B per agenti GUI. Supporta aspect-ratio dinamico per smartphone 20:9 con OCR nitido e rigorosa aderenza JSON."
        },
        new LocalModel
        {
            Id = "smolvlm2-2.2b-instruct",
            Name = "SmolVLM2-2.2B-Instruct",
            DisplayName = "SmolVLM2 2.2B (Ultra-Lightweight / Budget)",
            Provider = "LLamaSharp",
            Architecture = "idefics3",
            Quantization = "Q6_K",
            Quantization = "Q4_K_M",
            ParameterCount = "2.2B",
            ContextLength = 4096,
            FileSize = 2_090_000_000L,
            FileSize = 1_112_242_368L,
            RamRequirementMb = 3584,
            RecommendedRamRange = "4 - 8 GB",
            RamTier = RamTier.Tier8Gb,
            DownloadUrl = "https://huggingface.co/ggml-org/SmolVLM-Instruct-GGUF/resolve/main/SmolVLM-Instruct-Q6_K.gguf",
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
            Description = "Modello compatto ultra-leggero HuggingFaceTB con encoder SigLIP. Minimo consumo di memoria per laptop e PC datati con fallback CPU."
        },

        // =====================================================================
        // === Tier 16 GB (Balanced — RAM 8–16 GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen2.5-vl-7b-instruct",
            Name = "Qwen2.5-VL 7B Instruct",
            DisplayName = "Qwen2.5-VL 7B (Gold Standard GUI Agent)",
            Provider = "LLamaSharp",
            Architecture = "qwen2vl",
            Quantization = "Q5_K_M",
            Quantization = "Q4_K_M",
            ParameterCount = "7B",
            ContextLength = 8192,
            FileSize = 6_120_000_000L,
            FileSize = 4_683_072_032L,
            RamRequirementMb = 8192,
            RecommendedRamRange = "8 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/qwen2.5-vl-7b-instruct-q5_k_m.gguf",
            Checksum = "67d1659bfe71b89d50b45a4ad1a9e5b997e5bb16ce5da66a6a6167abd569e9e2",
            DownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf",
            Checksum = "9258bf05b12686d097ff3b6b18d968ab393649780aa2b3cd67fec43d50554392",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-qwen2.5-vl-7b-instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/mmproj-qwen2.5-vl-7b-instruct-f16.gguf",
            MmprojChecksum = "ca524100ebf825c9a870db1c580d03879e0da0ab2541697e2458e64891cf9d38",
            MmprojFileSize = 1_180_000_000L,
            MmprojFileName = "mmproj-Qwen2.5-VL-7B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/mmproj-Qwen2.5-VL-7B-Instruct-f16.gguf",
            MmprojChecksum = "c24a7f5fcfc68286f0a217023b6738e73bea4f11787a43e8238d4bb1b8604cde",
            MmprojFileSize = 1_354_162_912L,
            MmprojVersion = "2.5",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Gold standard per agenti GUI open-weight. Precisione millimetrica su pulsanti, lettura impeccabile di font minuscoli e schemi JSON privi di errori."
        },
        new LocalModel
        {
            Id = "internvl2.5-8b-instruct",
            Name = "InternVL2.5-8B-Instruct",
            DisplayName = "InternVL 2.5 8B (Dynamic High-Res Tiling)",
            Provider = "LLamaSharp",
            Architecture = "internvl",
            Quantization = "Q5_K_M",
            Quantization = "Q4_K_M",
            ParameterCount = "8B",
            ContextLength = 8192,
            FileSize = 6_010_000_000L,
            FileSize = 4_681_132_224L,
            RamRequirementMb = 9216,
            RecommendedRamRange = "10 - 16 GB",
            RamTier = RamTier.Tier16Gb,
            DownloadUrl = "https://huggingface.co/OpenGVLab/InternVL2_5-8B-GGUF/resolve/main/InternVL2_5-8B-Q5_K_M.gguf",
            DownloadUrl = "https://huggingface.co/ggml-org/InternVL3-8B-Instruct-GGUF/resolve/main/InternVL3-8B-Instruct-Q4_K_M.gguf",
            Checksum = "645b5db5754711f90adcc85dd99217a66d1306c97c2a2d3ae69071e3dea7a42e",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-InternVL2_5-8B-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/OpenGVLab/InternVL2_5-8B-GGUF/resolve/main/mmproj-InternVL2_5-8B-f16.gguf",
            MmprojFileName = "mmproj-InternVL3-8B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/InternVL3-8B-Instruct-GGUF/resolve/main/mmproj-InternVL3-8B-Instruct-f16.gguf",
            MmprojChecksum = "7ad5f0c3e49de0f63a50ebf22c0bb16961175154ca32604d3fb083602d9d5d51",
            MmprojFileSize = 666_002_720L,
            MmprojVersion = "2.5",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Frontier vision model OpenGVLab basato su dynamic high-res tiling. Superiore nel rilevamento di micro-icone, badge e dettagli HUD semitrasparenti."
        },

        // =====================================================================
        // === Tier 32 GB+ (Performance — RAM 16–32+ GB) ===
        // =====================================================================
        new LocalModel
        {
            Id = "qwen2.5-vl-72b-instruct",
            Name = "Qwen2.5-VL 72B Instruct",
            DisplayName = "Qwen2.5-VL 72B (Frontier Multimodal Reasoning)",
            Provider = "LLamaSharp",
            Architecture = "qwen2vl",
            Quantization = "Q4_K_M",
            ParameterCount = "72B",
            ContextLength = 8192,
            FileSize = 24_150_000_000L,
            FileSize = 47_415_713_248L,
            RamRequirementMb = 24576,
            RecommendedRamRange = "24 - 48 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-72B-Instruct-GGUF/resolve/main/qwen2.5-vl-72b-instruct-q4_k_m.gguf",
            Checksum = "87bb374d849f80ebdfabb304189fac9e0bd35a0f74506e6a59c51b206cbe863b",
            DownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-72B-Instruct-GGUF/resolve/main/Qwen2.5-VL-72B-Instruct-Q4_K_M.gguf",
            Checksum = "9f0dcad3b2c3b3b884323d46b7c21e9bce379175e66863e36c77586db6979986",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-qwen2.5-vl-72b-instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-VL-72B-Instruct-GGUF/resolve/main/mmproj-qwen2.5-vl-72b-instruct-f16.gguf",
            MmprojChecksum = "cae72cf123cc9e08d553cd5a5055d6d3cf0f82652aa41c3e4aa424cda9a26f7f",
            MmprojFileSize = 1_280_000_000L,
            MmprojFileName = "mmproj-Qwen2.5-VL-72B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/Qwen2.5-VL-72B-Instruct-GGUF/resolve/main/mmproj-Qwen2.5-VL-72B-Instruct-f16.gguf",
            MmprojChecksum = "6099885b9c4056e24806b616401ff2730a7354335e6f2f0eaf2a45e89c8a457c",
            MmprojFileSize = 1_410_222_816L,
            MmprojVersion = "2.5",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Fast,
            QualityTier = QualityTier.High,
            Description = "Capacità visiva e di ragionamento frontier paragonabile a modelli cloud. Zero allucinazioni, strategie complesse multi-step e totale assenza di loop."
        },
        new LocalModel
        {
            Id = "internvl2.5-26b-instruct",
            Name = "InternVL2.5 26B-MPO Instruct",
            DisplayName = "InternVL 2.5 26B (Dense High-Precision Vision)",
            Provider = "LLamaSharp",
            Architecture = "internvl",
            Quantization = "Q4_K_M",
            ParameterCount = "26B",
            ParameterCount = "14B",
            ContextLength = 8192,
            FileSize = 18_460_000_000L,
            RamRequirementMb = 20480,
            RecommendedRamRange = "24 - 48 GB",
            FileSize = 8_985_340_608L,
            RamRequirementMb = 16384,
            RecommendedRamRange = "16 - 32 GB",
            RamTier = RamTier.Tier32GbPlus,
            DownloadUrl = "https://huggingface.co/OpenGVLab/InternVL2_5-26B-MPO-GGUF/resolve/main/InternVL2_5-26B-MPO-Q4_K_M.gguf",
            Checksum = "5cf0136e721d6294718ec71fd8c93b17ab5dd4e2714d6079e83fa46571ad94c8",
            DownloadUrl = "https://huggingface.co/ggml-org/InternVL3-14B-Instruct-GGUF/resolve/main/InternVL3-14B-Instruct-Q4_K_M.gguf",
            Checksum = "4e68e1a798088391fb26051c4165a25cf1a20f76e3f5b891c024375d2bb8a30e",
            RequiresMmproj = true,
            MmprojFileName = "mmproj-InternVL2_5-26B-MPO-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/OpenGVLab/InternVL2_5-26B-MPO-GGUF/resolve/main/mmproj-InternVL2_5-26B-MPO-f16.gguf",
            MmprojChecksum = "8617824839df91f84b4840ad5084dcf50a1403a435a1f4cfc4d8c84ce6cac2fc",
            MmprojFileSize = 838_860_800L,
            MmprojFileName = "mmproj-InternVL3-14B-Instruct-f16.gguf",
            MmprojDownloadUrl = "https://huggingface.co/ggml-org/InternVL3-14B-Instruct-GGUF/resolve/main/mmproj-InternVL3-14B-Instruct-f16.gguf",
            MmprojChecksum = "cf5f50c22d33836cd985f29821c5ae1a5acde948f542c4a8c6cfaed8738a997b",
            MmprojFileSize = 705_336_640L,
            MmprojVersion = "2.5",
            SupportsVision = true,
            SupportsJsonSchema = true,
            Version = "2.5",
            LicenseName = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            SpeedTier = SpeedTier.Medium,
            QualityTier = QualityTier.High,
            Description = "Modello vision denso da 26B ottimizzato con Mixed Preference Optimization. Eccellente bilanciamento tra velocità e precisione spaziale sub-pixel."
            Description = "Modello vision denso da 14B/26B con precisione spaziale sub-pixel. Eccellente bilanciamento tra velocità e memoria per sistemi ad alte prestazioni."
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
        // Strictly returns the curated 2 recommended models in exact priority order per tier
        var targetIds = tier switch
        {
            RamTier.Tier8Gb => new[]
            {
                "qwen2.5-vl-3b-instruct",
                "smolvlm2-2.2b-instruct"
            },
            RamTier.Tier16Gb => new[]
            {
                "qwen2.5-vl-7b-instruct",
                "internvl2.5-8b-instruct"
            },
            _ => new[]
            {
                "qwen2.5-vl-72b-instruct",
                "internvl2.5-26b-instruct"
            }
        };

        var result = new List<LocalModel>(2);
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
