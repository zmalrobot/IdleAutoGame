using FluentAssertions;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class LinuxHardwareDetectorTests : IDisposable
{
    private readonly string _tempMemInfo;
    private readonly string _tempCpuInfo;

    public LinuxHardwareDetectorTests()
    {
        _tempMemInfo = Path.GetTempFileName();
        _tempCpuInfo = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempMemInfo)) File.Delete(_tempMemInfo);
        if (File.Exists(_tempCpuInfo)) File.Delete(_tempCpuInfo);
    }

    [Fact]
    public async Task DetectAsync_ParsesMemInfoAndCpuInfoAccurately()
    {
        // Simulated /proc/meminfo
        var memContent = """
        MemTotal:       16384000 kB
        MemFree:         4096000 kB
        MemAvailable:   12288000 kB
        """;
        await File.WriteAllTextAsync(_tempMemInfo, memContent);

        // Simulated /proc/cpuinfo
        var cpuContent = """
        processor	: 0
        model name	: AMD Ryzen 7 5800X 8-Core Processor
        cpu MHz		: 3800.000
        """;
        await File.WriteAllTextAsync(_tempCpuInfo, cpuContent);

        var detector = new LinuxHardwareDetector(_tempMemInfo, _tempCpuInfo);
        var info = await detector.DetectAsync();

        info.TotalRamMb.Should().Be(16000); // 16384000 / 1024
        info.AvailableRamMb.Should().Be(12000); // 12288000 / 1024
        info.CpuName.Should().Be("AMD Ryzen 7 5800X 8-Core Processor");
        info.CpuCores.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DetectAsync_NonExistentFiles_FallsBackGracefullyWithoutThrowing()
    {
        var detector = new LinuxHardwareDetector("non-existent-meminfo", "non-existent-cpuinfo");
        var info = await detector.DetectAsync();

        info.Should().NotBeNull();
        info.CpuName.Should().Be("Unknown CPU");
    }
}
