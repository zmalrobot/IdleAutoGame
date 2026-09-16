using System.Collections.Concurrent;
using System.Security.Cryptography;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Service managing discovery, storage, single-concurrency downloading, SHA-256 verification, and lifecycle of local LLM models.
/// </summary>
public sealed class ModelManager : IModelManager
{
    private readonly IModelCatalog _catalog;
    private readonly IModelDownloader _downloader;
    private readonly ISettingsRepository? _settingsRepo;
    private string? _customStorageDir;
    private readonly ConcurrentDictionary<string, bool> _inUseModels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeDownloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    /// <inheritdoc />
    public event EventHandler<ModelDownloadProgress>? DownloadProgressChanged;

    /// <inheritdoc />
    public event EventHandler<LocalModel>? ModelStatusChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="ModelManager"/>.
    /// </summary>
    public ModelManager(
        IModelCatalog catalog,
        IModelDownloader downloader,
        ISettingsRepository? settingsRepo = null,
        string? storageDirectory = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _settingsRepo = settingsRepo;
        _customStorageDir = storageDirectory;
    }

    /// <summary>
    /// Updates the runtime storage directory used for local models.
    /// </summary>
    public void SetModelStorageDirectory(string? storageDirectory)
    {
        _customStorageDir = storageDirectory;
    }

    /// <inheritdoc />
    public string GetModelStorageDirectory()
    {
        var dir = _customStorageDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = LlmSettings.DefaultModelStorageDirectory;
        }

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return dir;
    }

    /// <inheritdoc />
    public string GetModelFilePath(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        var dir = GetModelStorageDirectory();
        return Path.Combine(dir, $"{modelId}.gguf");
    }

    /// <inheritdoc />
    public bool IsModelInUse(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return _inUseModels.TryGetValue(modelId, out var inUse) && inUse;
    }

    /// <inheritdoc />
    public void MarkModelInUse(string modelId, bool inUse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        _inUseModels[modelId] = inUse;

        var model = _catalog.GetLocalModel(modelId);
        if (model != null)
        {
            if (inUse)
            {
                model.Status = ModelStatus.InUse;
            }
            else if (IsModelInstalled(modelId))
            {
                model.Status = ModelStatus.Ready;
            }

            ModelStatusChanged?.Invoke(this, model);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalModel>> GetAllModelsAsync(CancellationToken ct = default)
    {
        var all = _catalog.GetAllLocalModels();
        foreach (var m in all)
        {
            SyncModelDiskStatus(m);
        }

        return Task.FromResult<IReadOnlyList<LocalModel>>(all.ToList().AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModel>> GetInstalledModelsAsync(CancellationToken ct = default)
    {
        var all = await GetAllModelsAsync(ct).ConfigureAwait(false);
        return all.Where(m => m.Status is ModelStatus.Ready or ModelStatus.Loaded or ModelStatus.InUse)
                  .ToList()
                  .AsReadOnly();
    }

    /// <inheritdoc />
    public Task<LocalModel?> GetModelAsync(string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        var model = _catalog.GetLocalModel(modelId);
        if (model != null)
        {
            SyncModelDiskStatus(model);
        }

        return Task.FromResult(model);
    }

    /// <inheritdoc />
    public Task<bool> IsModelInstalledAsync(string modelId, CancellationToken ct = default)
    {
        return Task.FromResult(IsModelInstalled(modelId));
    }

    private bool IsModelInstalled(string modelId)
    {
        var path = GetModelFilePath(modelId);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    private void SyncModelDiskStatus(LocalModel model)
    {
        if (IsModelInUse(model.Id))
        {
            model.Status = ModelStatus.InUse;
            return;
        }

        if (_activeDownloads.ContainsKey(model.Id))
        {
            model.Status = ModelStatus.Downloading;
            return;
        }

        var path = GetModelFilePath(model.Id);
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            if (info.Length > 0)
            {
                model.FilePath = path;
                model.Status = ModelStatus.Ready;
                model.InstalledAt ??= info.CreationTimeUtc;
                return;
            }
        }

        model.FilePath = null;
        model.Status = ModelStatus.NotInstalled;
    }

    /// <inheritdoc />
    public async Task<LocalModel> DownloadAndInstallModelAsync(
        string modelId,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var model = _catalog.GetLocalModel(modelId)
            ?? throw new ArgumentException($"Model '{modelId}' not found in catalog.", nameof(modelId));

        // Enforce single download concurrency
        if (!await _downloadLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another model download is already in progress. Concurrent downloads are disabled.");
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeDownloads[modelId] = cts;

        var targetDir = GetModelStorageDirectory();
        var finalPath = GetModelFilePath(modelId);
        var tempPath = finalPath + ".tmp";

        try
        {
            // 1. Verify available disk space
            CheckDiskSpace(targetDir, model.FileSize);

            // 2. Set Downloading state
            model.Status = ModelStatus.Downloading;
            ModelStatusChanged?.Invoke(this, model);

            var forwardProgress = new Progress<ModelDownloadProgress>(p =>
            {
                DownloadProgressChanged?.Invoke(this, p);
                progress?.Report(p);
            });

            // 3. Perform download
            await _downloader.DownloadModelAsync(model, tempPath, forwardProgress, cts.Token).ConfigureAwait(false);

            // 4. Verify checksum
            model.Status = ModelStatus.Verifying;
            ModelStatusChanged?.Invoke(this, model);

            if (!string.IsNullOrWhiteSpace(model.Checksum))
            {
                var isValid = await VerifyChecksumAsync(tempPath, model.Checksum, cts.Token).ConfigureAwait(false);
                if (!isValid)
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }

                    model.Status = ModelStatus.Error;
                    ModelStatusChanged?.Invoke(this, model);
                    throw new InvalidOperationException($"Checksum verification failed for model '{modelId}'. File was removed.");
                }
            }

            // 5. Finalize installation (atomic rename)
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);

            model.FilePath = finalPath;
            model.InstalledAt = DateTimeOffset.UtcNow;
            model.Status = ModelStatus.Ready;
            ModelStatusChanged?.Invoke(this, model);

            return model;
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
            }

            model.Status = ModelStatus.NotInstalled;
            ModelStatusChanged?.Invoke(this, model);
            throw;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
            }

            model.Status = ModelStatus.Error;
            ModelStatusChanged?.Invoke(this, model);
            throw;
        }
        finally
        {
            _activeDownloads.TryRemove(modelId, out _);
            _downloadLock.Release();
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public Task CancelDownloadAsync(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (_activeDownloads.TryGetValue(modelId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteModelAsync(string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (IsModelInUse(modelId))
        {
            throw new InvalidOperationException($"Cannot delete model '{modelId}' because it is currently in use.");
        }

        var model = _catalog.GetLocalModel(modelId);
        if (model != null)
        {
            model.Status = ModelStatus.Deleting;
            ModelStatusChanged?.Invoke(this, model);
        }

        var finalPath = GetModelFilePath(modelId);
        var tempPath = finalPath + ".tmp";

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        if (model != null)
        {
            model.FilePath = null;
            model.InstalledAt = null;
            model.Status = ModelStatus.NotInstalled;
            ModelStatusChanged?.Invoke(this, model);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyModelIntegrityAsync(string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var model = _catalog.GetLocalModel(modelId);
        if (model == null || string.IsNullOrWhiteSpace(model.Checksum))
        {
            return false;
        }

        var path = GetModelFilePath(modelId);
        if (!File.Exists(path))
        {
            return false;
        }

        return await VerifyChecksumAsync(path, model.Checksum, ct).ConfigureAwait(false);
    }

    private static void CheckDiskSpace(string targetDirectory, long requiredBytes)
    {
        try
        {
            var fullPath = Path.GetFullPath(targetDirectory);
            var driveInfo = new DriveInfo(Path.GetPathRoot(fullPath) ?? fullPath);

            // Reserve 500 MB safety buffer on disk
            long safetyBuffer = 500L * 1024 * 1024;
            if (driveInfo.AvailableFreeSpace < (requiredBytes + safetyBuffer))
            {
                double freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                double reqGb = requiredBytes / (1024.0 * 1024 * 1024);
                throw new InvalidOperationException(
                    $"Insufficient disk space on {driveInfo.Name}. Available: {freeGb:F1} GB, Required: {reqGb:F1} GB (plus buffer).");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // If DriveInfo fails on non-standard mount paths, skip check
        }
    }

    private static async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        var computedHex = Convert.ToHexString(hashBytes);

        return string.Equals(computedHex, expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
