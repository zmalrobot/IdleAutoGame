namespace IdleAutoGame.Core.Models;

/// <summary>
/// A normalized bounding rectangle where coordinates and dimensions are relative [0.0 - 1.0].
/// </summary>
/// <param name="X">Normalized top-left X coordinate.</param>
/// <param name="Y">Normalized top-left Y coordinate.</param>
/// <param name="Width">Normalized width.</param>
/// <param name="Height">Normalized height.</param>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// Checks if a normalized point falls inside the rectangle.
    /// </summary>
    public bool Contains(double px, double py) =>
        px >= X && px <= (X + Width) && py >= Y && py <= (Y + Height);
}

