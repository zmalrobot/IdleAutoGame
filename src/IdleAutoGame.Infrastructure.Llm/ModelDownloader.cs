using System.Diagnostics;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// HTTP downloader for local GGUF model files with chunked streaming, progress, speed calculation, and cancellation.
/// </summary>
public sealed class ModelDownloader : IModelDownloader
{
    private readonly HttpClient _httpClient;
    private const int BufferSize = 64 * 1024; // 64 KB

    /// <summary>
    /// Initializes a new instance of <see cref="ModelDownloader"/>.
    /// </summary>
    public ModelDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public async Task DownloadModelAsync(
        LocalModel model,
        string destinationPath,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (string.IsNullOrWhiteSpace(model.DownloadUrl))
        {
            throw new InvalidOperationException($"Model '{model.Id}' does not specify a DownloadUrl.");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(
            model.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? model.FileSize;
        if (totalBytes <= 0)
        {
            totalBytes = model.FileSize;
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous);

        var buffer = new byte[BufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        var stopwatch = Stopwatch.StartNew();
        var lastReportStopwatch = Stopwatch.StartNew();
        long lastReportBytes = 0;
        double currentSpeed = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            totalBytesRead += bytesRead;

            // Report progress at sensible intervals (every ~250ms) or when finished
            if (lastReportStopwatch.ElapsedMilliseconds >= 250 || totalBytesRead >= totalBytes)
            {
                double elapsedSec = lastReportStopwatch.Elapsed.TotalSeconds;
                if (elapsedSec > 0.05)
                {
                    long deltaBytes = totalBytesRead - lastReportBytes;
                    currentSpeed = deltaBytes / elapsedSec;
                    lastReportBytes = totalBytesRead;
                    lastReportStopwatch.Restart();
                }

                TimeSpan? eta = null;
                if (currentSpeed > 0 && totalBytes > totalBytesRead)
                {
                    double remainingSec = (totalBytes - totalBytesRead) / currentSpeed;
                    eta = TimeSpan.FromSeconds(Math.Min(remainingSec, 86400 * 7)); // Cap at 7 days
                }

                double percentage = totalBytes > 0
                    ? Math.Min(100.0, (double)totalBytesRead / totalBytes * 100.0)
                    : 0.0;

                progress?.Report(new ModelDownloadProgress
                {
                    ModelId = model.Id,
                    BytesDownloaded = totalBytesRead,
                    TotalBytes = totalBytes,
                    Percentage = percentage,
                    SpeedBytesPerSec = currentSpeed,
                    EstimatedRemaining = eta,
                    Status = ModelStatus.Downloading
                });
            }
        }

        // Final 100% progress report
        progress?.Report(new ModelDownloadProgress
        {
            ModelId = model.Id,
            BytesDownloaded = totalBytesRead,
            TotalBytes = Math.Max(totalBytes, totalBytesRead),
            Percentage = 100.0,
            SpeedBytesPerSec = 0,
            EstimatedRemaining = TimeSpan.Zero,
            Status = ModelStatus.Verifying
        });
    }
}

