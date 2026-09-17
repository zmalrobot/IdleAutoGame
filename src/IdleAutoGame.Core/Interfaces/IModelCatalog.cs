using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Catalog providing model profiles, hardware prerequisites, and compatibility recommendations.
/// </summary>
public interface IModelCatalog
{
    /// <summary>
    /// Gets all registered model profiles (both local and cloud).
    /// </summary>
    IReadOnlyList<ModelProfile> GetAllModels();

    /// <summary>
    /// Filters and returns model profiles compatible with the specified hardware capabilities.
    /// </summary>
    IReadOnlyList<ModelProfile> GetCompatibleModels(HardwareInfo hardware);

    /// <summary>
    /// Finds a model profile by its identifier.
    /// </summary>
    ModelProfile? GetModel(string modelId);

    /// <summary>
    /// Gets all registered local GGUF models from the catalog.
    /// </summary>
    IReadOnlyList<LocalModel> GetAllLocalModels();

    /// <summary>
    /// Returns exactly the 4 recommended local models for the designated RAM tier.
    /// </summary>
    IReadOnlyList<LocalModel> GetRecommendedModelsForTier(RamTier tier);

    /// <summary>
    /// Classifies detected hardware into a standard RAM tier.
    /// </summary>
    RamTier DetermineRamTier(HardwareInfo hardware);

    /// <summary>
    /// Finds a local model by its identifier.
    /// </summary>
    LocalModel? GetLocalModel(string id);

    /// <summary>
    /// Migrates a legacy model ID to its modern equivalent if recognized; otherwise returns the original ID.
    /// </summary>
    string MigrateModelId(string modelId);
}
