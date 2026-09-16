using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Service coordinating discovery, downloading, verification, installation, and lifecycle of local LLM models.
/// </summary>
public interface IModelManager
{
    /// <summary>
    /// Occurs when download progress updates for an in-flight model download.
    /// </summary>
    event EventHandler<ModelDownloadProgress>? DownloadProgressChanged;

    /// <summary>
    /// Occurs when the status of a local model transitions.
    /// </summary>
    event EventHandler<LocalModel>? ModelStatusChanged;

    /// <summary>
    /// Gets all models known to the system, annotated with their current local installation status.
    /// </summary>
    Task<IReadOnlyList<LocalModel>> GetAllModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all models that are fully downloaded, verified, and ready on disk.
    /// </summary>
    Task<IReadOnlyList<LocalModel>> GetInstalledModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a specific local model by identifier, including current disk/runtime status.
    /// </summary>
    Task<LocalModel?> GetModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the model GGUF file is physically present and valid on disk.
    /// </summary>
    Task<bool> IsModelInstalledAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Downloads, verifies checksum, and installs a catalog model into local storage.
    /// </summary>
    Task<LocalModel> DownloadAndInstallModelAsync(
        string modelId,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels an in-flight download and cleans up any partial temporary files.
    /// </summary>
    Task CancelDownloadAsync(string modelId);

    /// <summary>
    /// Safely deletes an installed model from local storage.
    /// Throws an exception if the model is currently in use by an active inference session.
    /// </summary>
    Task DeleteModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Verifies the SHA-256 integrity of an installed model against catalog metadata.
    /// </summary>
    Task<bool> VerifyModelIntegrityAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a model is currently marked as active or executing inference.
    /// </summary>
    bool IsModelInUse(string modelId);

    /// <summary>
    /// Sets whether a model is currently locked by the inference runtime.
    /// </summary>
    void MarkModelInUse(string modelId, bool inUse);

    /// <summary>
    /// Gets the absolute path of the configured local model storage directory.
    /// </summary>
    string GetModelStorageDirectory();

    /// <summary>
    /// Gets the absolute file path where the specified model GGUF file is located.
    /// </summary>
    string GetModelFilePath(string modelId);
}

