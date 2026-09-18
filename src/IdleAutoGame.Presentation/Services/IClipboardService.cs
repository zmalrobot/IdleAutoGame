using System.Threading.Tasks;

namespace IdleAutoGame.Presentation.Services;

/// <summary>
/// Service abstraction for interacting with the system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Asynchronously copies text to the system clipboard.
    /// </summary>
    /// <param name="text">The text content to copy.</param>
    Task SetTextAsync(string? text);

    /// <summary>
    /// Asynchronously retrieves text from the system clipboard.
    /// </summary>
    Task<string?> GetTextAsync();

    /// <summary>
    /// Asynchronously copies image bytes (PNG) to the system clipboard.
    /// </summary>
    /// <param name="imageBytes">The raw image byte array.</param>
    Task SetImageAsync(byte[] imageBytes);
}

