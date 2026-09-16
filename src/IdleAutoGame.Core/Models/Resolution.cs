namespace IdleAutoGame.Core.Models;

/// <summary>
/// Physical screen resolution of a device in pixels.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct Resolution(int Width, int Height)
{
    /// <summary>
    /// Gets an empty (zero-sized) resolution.
    /// </summary>
    public static Resolution Empty => new(0, 0);

    /// <summary>
    /// Gets a value indicating whether the resolution has non-zero dimensions.
    /// </summary>
    public bool IsValid => Width > 0 && Height > 0;

    /// <inheritdoc />
    public override string ToString() => $"{Width}x{Height}";
}

