using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Service abstraction responsible for downloading GGUF model files over HTTP with progress and cancellation.
/// </summary>
public interface IModelDownloader
{
    /// <summary>
    /// Downloads a model payload from its remote URL to the target file path.
    /// </summary>
    /// <param name="model">Model metadata containing DownloadUrl and FileSize.</param>
    /// <param name="destinationPath">Target disk destination file path.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadModelAsync(
        LocalModel model,
        string destinationPath,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads an asset file from an arbitrary remote URL to a local destination file path.
    /// </summary>
    /// <param name="url">The remote download URL.</param>
    /// <param name="expectedSize">Expected file size in bytes if known.</param>
    /// <param name="destinationPath">Target local destination path.</param>
    /// <param name="modelId">Associated model identifier for progress reporting.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadFileAsync(
        string url,
        long expectedSize,
        string destinationPath,
        string modelId,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken ct = default);
}

