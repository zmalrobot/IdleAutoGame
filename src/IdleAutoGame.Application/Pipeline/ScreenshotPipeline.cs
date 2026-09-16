using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Pipeline;

/// <summary>
/// Preprocessing pipeline for device screenshots prior to LLM analysis.
/// </summary>
public static class ScreenshotPipeline
{
    /// <summary>
    /// Validates frame integrity and extracts base64 encoded payload.
    /// </summary>
    /// <param name="screenshot">The captured screenshot data.</param>
    /// <param name="base64Payload">Resulting base64 string.</param>
    /// <param name="error">Error message if frame is invalid.</param>
    /// <returns>True if screenshot is valid for inference; otherwise false.</returns>
    public static bool Process(ScreenshotData? screenshot, out string base64Payload, out string? error)
    {
        base64Payload = string.Empty;
        error = null;

        if (screenshot == null)
        {
            error = "Screenshot data is null.";
            return false;
        }

        if (screenshot.ImageBytes == null || screenshot.ImageBytes.Length == 0)
        {
            error = "Screenshot contains empty or null image byte array.";
            return false;
        }

        if (screenshot.Width <= 0 || screenshot.Height <= 0)
        {
            error = $"Invalid screenshot resolution: {screenshot.Width}x{screenshot.Height}.";
            return false;
        }

        base64Payload = screenshot.ToBase64();
        return true;
    }
}

