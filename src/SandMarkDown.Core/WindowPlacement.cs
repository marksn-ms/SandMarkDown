using System.Text.Json;

namespace SandMarkDown.Core;

/// <summary>
/// Size and position of the reader window, remembered between sessions.
/// Units are device-independent pixels, as WPF reports them.
/// </summary>
/// <param name="Left">Left edge.</param>
/// <param name="Top">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="IsMaximized">True when the window was maximized.</param>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Reads a placement from <paramref name="path"/>.
    /// </summary>
    /// <param name="path">JSON file written by <see cref="Save"/>.</param>
    /// <returns>The saved placement, or <see langword="null"/> when the file is missing or unusable.</returns>
    public static WindowPlacement? Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var placement = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(path), JsonOptions);
            return placement is not null && placement.IsUsable() ? placement : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes this placement to <paramref name="path"/>, creating the folder when needed.
    /// </summary>
    /// <param name="path">JSON file to write.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>
    /// Fits this placement onto the visible desktop. A window that would be off screen, for example after
    /// a monitor was unplugged, is moved back and shrunk to fit.
    /// </summary>
    /// <param name="screenLeft">Left edge of the virtual desktop.</param>
    /// <param name="screenTop">Top edge of the virtual desktop.</param>
    /// <param name="screenWidth">Width of the virtual desktop.</param>
    /// <param name="screenHeight">Height of the virtual desktop.</param>
    /// <param name="minWidth">Smallest allowed window width.</param>
    /// <param name="minHeight">Smallest allowed window height.</param>
    /// <returns>A placement that is at least mostly visible.</returns>
    public WindowPlacement FitTo(
        double screenLeft,
        double screenTop,
        double screenWidth,
        double screenHeight,
        double minWidth,
        double minHeight)
    {
        var width = Math.Clamp(Width, Math.Min(minWidth, screenWidth), screenWidth);
        var height = Math.Clamp(Height, Math.Min(minHeight, screenHeight), screenHeight);
        var left = Math.Clamp(Left, screenLeft, screenLeft + screenWidth - width);
        var top = Math.Clamp(Top, screenTop, screenTop + screenHeight - height);
        return this with { Left = left, Top = top, Width = width, Height = height };
    }

    private bool IsUsable() =>
        double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height)
        && Width > 0 && Height > 0;
}
