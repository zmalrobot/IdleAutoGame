using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class SplashViewModel : ViewModelBase
{
    private readonly IHardwareDetector _hardwareDetector;
    private readonly Action _onReady;

    [ObservableProperty]
    private string _statusText = "Checking system environment...";

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _hardwareSummary = string.Empty;

    [ObservableProperty]
    private HardwareInfo _hardware = HardwareInfo.Empty;

    public SplashViewModel(IHardwareDetector hardwareDetector, Action onReady)
    {
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _onReady = onReady ?? throw new ArgumentNullException(nameof(onReady));
    }

    [RelayCommand]
    public async Task RunPreflightCheckAsync()
    {
        StatusText = "Detecting host CPU, RAM, and GPU capabilities...";

        try
        {
            Hardware = await _hardwareDetector.DetectAsync();
            var vramText = Hardware.VramMb.HasValue ? $"{Hardware.VramMb.Value / 1024.0:F1} GB VRAM" : "Integrated/No VRAM";
            HardwareSummary = $"CPU: {Hardware.CpuName} ({Hardware.CpuCores} cores)\n" +
                              $"RAM: {Hardware.TotalRamMb / 1024.0:F1} GB total ({Hardware.AvailableRamMb / 1024.0:F1} GB free)\n" +
                              $"GPU: {Hardware.GpuName ?? "Standard GPU"} ({vramText})";

            StatusText = "Pre-flight checks passed successfully.";
            IsCompleted = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Preflight check warning: {ex.Message}. Using fallback configuration.";
            HardwareSummary = "Fallback hardware profile applied.";
            IsCompleted = true;
        }
    }

    [RelayCommand]
    public void Continue()
    {
        _onReady();
    }
}

