namespace SandMarkDown.Core;

/// <summary>
/// Zoom steps for the Markdown reading surface.
/// </summary>
public static class ZoomMath
{
    /// <summary>
    /// Smallest allowed zoom factor. 1.0 is actual size.
    /// </summary>
    public const double Minimum = 0.5;

    /// <summary>
    /// Largest allowed zoom factor.
    /// </summary>
    public const double Maximum = 3.0;

    /// <summary>
    /// Change applied by one zoom-in or zoom-out step.
    /// </summary>
    public const double Step = 0.1;

    /// <summary>
    /// Actual size.
    /// </summary>
    public const double Actual = 1.0;

    /// <summary>
    /// Moves one step toward <see cref="Maximum"/>.
    /// </summary>
    /// <param name="current">Current zoom factor.</param>
    /// <returns>The next zoom factor.</returns>
    public static double ZoomIn(double current) =>
        Math.Min(Maximum, Math.Round(current + Step, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Moves one step toward <see cref="Minimum"/>.
    /// </summary>
    /// <param name="current">Current zoom factor.</param>
    /// <returns>The next zoom factor.</returns>
    public static double ZoomOut(double current) =>
        Math.Max(Minimum, Math.Round(current - Step, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Formats a zoom factor as a whole-number percent.
    /// </summary>
    /// <param name="factor">Zoom factor, where 1.0 is 100%.</param>
    /// <returns>A label such as <c>100%</c>.</returns>
    public static string FormatPercent(double factor)
    {
        var percent = Math.Round(factor * 100, MidpointRounding.AwayFromZero);
        return $"{percent:0}%";
    }
}
