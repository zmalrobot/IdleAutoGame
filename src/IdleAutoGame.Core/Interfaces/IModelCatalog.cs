using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Catalog providing model profiles, hardware prerequisites, and compatibility recommendations.
/// </summary>
public interface IModelCatalog
{
    /// <summary>
    /// Gets all registered model profiles.
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
}

