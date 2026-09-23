using IdleAutoGame.Core.Models;
using SkiaSharp;

namespace IdleAutoGame.Application.Pipeline;

/// <summary>
/// Preprocessing pipeline for device screenshots prior to LLM analysis.
/// Downscales oversized frames to preserve vision model inference speed and memory budgets.
/// </summary>
public static class ScreenshotPipeline
{
    private const int MaxDimension = 1024;

    /// <summary>
    /// Validates frame integrity, applies adaptive downscaling for vision models, and extracts base64 encoded payload.
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

        // Downscale frames if they exceed MaxDimension to keep visual tokens manageable (~400-600 tokens vs 2500+)
        if (screenshot.Width > MaxDimension || screenshot.Height > MaxDimension)
        {
            try
            {
                using var original = SKBitmap.Decode(screenshot.ImageBytes);
                if (original != null)
                {
                    float scale = Math.Min((float)MaxDimension / original.Width, (float)MaxDimension / original.Height);
                    int targetWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
                    int targetHeight = Math.Max(1, (int)Math.Round(original.Height * scale));

                    var imageInfo = new SKImageInfo(targetWidth, targetHeight, original.ColorType, original.AlphaType);
                    using var resized = original.Resize(imageInfo, SKFilterQuality.Medium);
                    if (resized != null)
                    {
                        using var image = SKImage.FromBitmap(resized);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 85);
                        base64Payload = Convert.ToBase64String(data.ToArray());
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback to unscaled bytes if SkiaSharp resize encounters an unexpected error
            }
        }

        base64Payload = screenshot.ToBase64();
        return true;
    }
}
