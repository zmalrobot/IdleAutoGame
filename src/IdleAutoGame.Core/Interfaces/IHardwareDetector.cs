using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Probes host machine hardware resources (RAM, CPU, GPU, VRAM) on Linux.
/// </summary>
public interface IHardwareDetector
{
    /// <summary>
    /// Probes and returns current system hardware specifications.
    /// </summary>
    Task<HardwareInfo> DetectAsync(CancellationToken ct = default);
}

