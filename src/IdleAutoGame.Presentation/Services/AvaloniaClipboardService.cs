using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace IdleAutoGame.Presentation.Services;

/// <summary>
/// Avalonia-based implementation of <see cref="IClipboardService"/>.
/// Safely interacts with the application lifetime's main or active top level clipboard.
/// </summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    /// <inheritdoc />
    public async Task SetTextAsync(string? text)
    {
        try
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.Windows.Count > 0 ? (desktop.Windows[^1] ?? desktop.MainWindow) : desktop.MainWindow;
                var clipboard = window?.Clipboard ?? desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text ?? string.Empty).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // Headless or platform clipboard failure suppressed gracefully
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetTextAsync()
    {
        try
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.Windows.Count > 0 ? (desktop.Windows[^1] ?? desktop.MainWindow) : desktop.MainWindow;
                var clipboard = window?.Clipboard ?? desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    return await clipboard.TryGetTextAsync().ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // Headless fallback
        }
        return null;
    }
}
