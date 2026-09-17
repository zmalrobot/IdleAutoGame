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
        _testDir = Path.Combine(Path.GetTempPath(), "IdleAutoGame_ModelManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _catalog = new JsonModelCatalog("non-existent-models.json");
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
    public async Task IsModelInstalledAsync_WhenFilesMissing_ReturnsFalse()
    {
        var manager = CreateManager();
        var installed = await manager.IsModelInstalledAsync("qwen3-vl-2b-instruct");

        installed.Should().BeFalse();
    }

    [Fact]
    public async Task IsModelInstalledAsync_WhenBaseExistsButMmprojMissing_ReturnsFalse()
    {
        var manager = CreateManager();
        var filePath = manager.GetModelFilePath("qwen3-vl-2b-instruct");
        await File.WriteAllTextAsync(filePath, "dummy base gguf content");

        // Base exists, but mmproj is missing -> not fully installed
        var installed = await manager.IsModelInstalledAsync("qwen3-vl-2b-instruct");
        installed.Should().BeFalse();
    }

    [Fact]
    public async Task IsModelInstalledAsync_WhenBothBaseAndMmprojExist_ReturnsTrue()
    {
        var manager = CreateManager();
        var filePath = manager.GetModelFilePath("qwen3-vl-2b-instruct");
        var mmprojPath = manager.GetMmprojFilePath("qwen3-vl-2b-instruct");

        await File.WriteAllTextAsync(filePath, "dummy base gguf content");
        await File.WriteAllTextAsync(mmprojPath, "dummy mmproj gguf content");

        var installed = await manager.IsModelInstalledAsync("qwen3-vl-2b-instruct");
        installed.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndInstallModelAsync_MultiAssetDownload_VerifiesAndInstallsBoth()
    {
        var model = _catalog.GetLocalModel("qwen3-vl-2b-instruct")!;

        byte[] basePayload = "test base model payload"u8.ToArray();
        string baseHex = Convert.ToHexString(SHA256.HashData(basePayload));

        byte[] mmprojPayload = "test vision projector payload"u8.ToArray();
        string mmprojHex = Convert.ToHexString(SHA256.HashData(mmprojPayload));

        var customCatalog = Substitute.For<IModelCatalog>();
        var testModel = model with
        {
            Checksum = baseHex,
            FileSize = basePayload.Length,
            MmprojChecksum = mmprojHex,
            MmprojFileSize = mmprojPayload.Length
        };
        customCatalog.GetLocalModel(testModel.Id).Returns(testModel);

        var customDownloader = Substitute.For<IModelDownloader>();
        customDownloader.DownloadModelAsync(Arg.Any<LocalModel>(), Arg.Any<string>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var destPath = callInfo.ArgAt<string>(1);
                await File.WriteAllBytesAsync(destPath, basePayload);
            });

        customDownloader.DownloadFileAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var destPath = callInfo.ArgAt<string>(2);
                await File.WriteAllBytesAsync(destPath, mmprojPayload);
            });

        var customManager = new ModelManager(customCatalog, customDownloader, storageDirectory: _testDir);

        var result = await customManager.DownloadAndInstallModelAsync(testModel.Id);

        result.Status.Should().Be(ModelStatus.Ready);
        result.FilePath.Should().NotBeNull();
        File.Exists(result.FilePath).Should().BeTrue();
        File.Exists(result.MmprojFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndInstallModelAsync_BaseChecksumMismatch_DeletesFilesAndThrows()
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
        var modelId = "qwen3-vl-2b-instruct";

        manager.MarkModelInUse(modelId, true);

        var act = async () => await manager.DeleteModelAsync(modelId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*currently in use*");
    }

    [Fact]
    public async Task DeleteModelAsync_WhenNotInstalledOrNotInUse_DeletesBothBaseAndMmprojFiles()
    {
        var manager = CreateManager();
        var modelId = "qwen3-vl-2b-instruct";
        var filePath = manager.GetModelFilePath(modelId);
        var mmprojPath = manager.GetMmprojFilePath(modelId);

        await File.WriteAllTextAsync(filePath, "content");
        await File.WriteAllTextAsync(mmprojPath, "mmproj content");

        File.Exists(filePath).Should().BeTrue();
        File.Exists(mmprojPath).Should().BeTrue();

        await manager.DeleteModelAsync(modelId);

        File.Exists(filePath).Should().BeFalse();
        File.Exists(mmprojPath).Should().BeFalse();
        (await manager.IsModelInstalledAsync(modelId)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyModelIntegrityAsync_VerifiesBothAssets()
    {
        var model = _catalog.GetLocalModel("qwen3-vl-2b-instruct")!;

        byte[] basePayload = "test base model payload"u8.ToArray();
        string baseHex = Convert.ToHexString(SHA256.HashData(basePayload));

        byte[] mmprojPayload = "test vision projector payload"u8.ToArray();
        string mmprojHex = Convert.ToHexString(SHA256.HashData(mmprojPayload));

        var customCatalog = Substitute.For<IModelCatalog>();
        var testModel = model with
        {
            Checksum = baseHex,
            MmprojChecksum = mmprojHex
        };
        customCatalog.GetLocalModel(testModel.Id).Returns(testModel);

        var manager = new ModelManager(customCatalog, _downloader, storageDirectory: _testDir);
        var filePath = manager.GetModelFilePath(testModel.Id);
        var mmprojPath = manager.GetMmprojFilePath(testModel.Id);

        await File.WriteAllBytesAsync(filePath, basePayload);
        await File.WriteAllBytesAsync(mmprojPath, mmprojPayload);

        var valid = await manager.VerifyModelIntegrityAsync(testModel.Id);
        valid.Should().BeTrue();
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
