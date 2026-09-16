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
}

