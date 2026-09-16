using System.Security.Cryptography;
using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class ModelManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly IModelCatalog _catalog;
    private readonly IModelDownloader _downloader;

    public ModelManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "IdleAutoGame_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _catalog = new JsonModelCatalog();
        _downloader = Substitute.For<IModelDownloader>();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private ModelManager CreateManager()
    {
        return new ModelManager(_catalog, _downloader, storageDirectory: _testDir);
    }

    [Fact]
    public async Task IsModelInstalledAsync_WhenFileMissing_ReturnsFalse()
    {
        var manager = CreateManager();
        var installed = await manager.IsModelInstalledAsync("moondream2-2b-q4");

        installed.Should().BeFalse();
    }

    [Fact]
    public async Task IsModelInstalledAsync_WhenFileExists_ReturnsTrue()
    {
        var manager = CreateManager();
        var filePath = manager.GetModelFilePath("moondream2-2b-q4");
        await File.WriteAllTextAsync(filePath, "dummy gguf content");

        var installed = await manager.IsModelInstalledAsync("moondream2-2b-q4");
        installed.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndInstallModelAsync_ValidDownload_VerifiesAndInstalls()
    {
        var manager = CreateManager();
        var model = _catalog.GetLocalModel("moondream2-2b-q4")!;

        // Configure downloader to write content matching the model's expected checksum
        byte[] payload = "test model payload"u8.ToArray();
        string expectedHex = Convert.ToHexString(SHA256.HashData(payload));

        // Create a test catalog returning a model with our expected payload checksum
        var customCatalog = Substitute.For<IModelCatalog>();
        var testModel = model with { Checksum = expectedHex, FileSize = payload.Length };
        customCatalog.GetLocalModel(testModel.Id).Returns(testModel);

        var customDownloader = Substitute.For<IModelDownloader>();
        customDownloader.DownloadModelAsync(Arg.Any<LocalModel>(), Arg.Any<string>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var destPath = callInfo.ArgAt<string>(1);
                await File.WriteAllBytesAsync(destPath, payload);
            });

        var customManager = new ModelManager(customCatalog, customDownloader, storageDirectory: _testDir);

        var result = await customManager.DownloadAndInstallModelAsync(testModel.Id);

        result.Status.Should().Be(ModelStatus.Ready);
        result.FilePath.Should().NotBeNull();
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndInstallModelAsync_ChecksumMismatch_DeletesFileAndThrows()
    {
        var customCatalog = Substitute.For<IModelCatalog>();
        var testModel = new LocalModel
        {
            Id = "corrupt-model",
            Name = "Corrupt Model",
            DisplayName = "Corrupt Model",
            Checksum = "0000000000000000000000000000000000000000000000000000000000000000",
            FileSize = 100,
            DownloadUrl = "http://localhost/model.gguf"
        };
        customCatalog.GetLocalModel(testModel.Id).Returns(testModel);

        var customDownloader = Substitute.For<IModelDownloader>();
        customDownloader.DownloadModelAsync(Arg.Any<LocalModel>(), Arg.Any<string>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var destPath = callInfo.ArgAt<string>(1);
                await File.WriteAllBytesAsync(destPath, "wrong content"u8.ToArray());
            });

        var manager = new ModelManager(customCatalog, customDownloader, storageDirectory: _testDir);

        var act = async () => await manager.DownloadAndInstallModelAsync(testModel.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Checksum verification failed*");

        var targetFile = manager.GetModelFilePath(testModel.Id);
        File.Exists(targetFile).Should().BeFalse();
        File.Exists(targetFile + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task DeleteModelAsync_WhenModelInUse_ThrowsInvalidOperationException()
    {
        var manager = CreateManager();
        var modelId = "moondream2-2b-q4";

        manager.MarkModelInUse(modelId, true);

        var act = async () => await manager.DeleteModelAsync(modelId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*currently in use*");
    }

    [Fact]
    public async Task DeleteModelAsync_WhenNotInstalledOrNotInUse_DeletesFile()
    {
        var manager = CreateManager();
        var modelId = "moondream2-2b-q4";
        var filePath = manager.GetModelFilePath(modelId);
        await File.WriteAllTextAsync(filePath, "content");

        File.Exists(filePath).Should().BeTrue();

        await manager.DeleteModelAsync(modelId);

        File.Exists(filePath).Should().BeFalse();
        (await manager.IsModelInstalledAsync(modelId)).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstallModelAsync_ConcurrentDownloads_ThrowsInvalidOperationException()
    {
        var customCatalog = Substitute.For<IModelCatalog>();
        var model = new LocalModel
        {
            Id = "slow-model",
            Name = "Slow Model",
            DisplayName = "Slow Model",
            FileSize = 1000,
            DownloadUrl = "http://localhost/slow.gguf"
        };
        customCatalog.GetLocalModel(model.Id).Returns(model);

        var tcs = new TaskCompletionSource<bool>();
        var slowDownloader = Substitute.For<IModelDownloader>();
        slowDownloader.DownloadModelAsync(Arg.Any<LocalModel>(), Arg.Any<string>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await tcs.Task;
            });

        var manager = new ModelManager(customCatalog, slowDownloader, storageDirectory: _testDir);

        // Start first download (will await tcs)
        var firstDownloadTask = manager.DownloadAndInstallModelAsync(model.Id);

        // Second download while first is in-flight must be rejected
        var actSecond = async () => await manager.DownloadAndInstallModelAsync(model.Id);
        await actSecond.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Another model download is already in progress*");

        // Release first download
        tcs.SetResult(true);
        try
        {
            await firstDownloadTask;
        }
        catch
        {
            // Expected if dummy file wasn't written
        }
    }
}

