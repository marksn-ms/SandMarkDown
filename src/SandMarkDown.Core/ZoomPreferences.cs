using System.Text.Json;

namespace SandMarkDown.Core;

/// <summary>
/// Remembers the reading zoom between sessions.
/// </summary>
public static class ZoomPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Reads a zoom factor from <paramref name="path"/>.
    /// A missing, unreadable, or out-of-range file returns <see cref="ZoomMath.Actual"/>.
    /// </summary>
    /// <param name="path">JSON file written by <see cref="Save"/>.</param>
    /// <returns>A factor between <see cref="ZoomMath.Minimum"/> and <see cref="ZoomMath.Maximum"/>.</returns>
    public static double Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return ZoomMath.Actual;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredZoom>(File.ReadAllText(path), JsonOptions);
            if (stored is null || !IsAllowed(stored.Zoom))
            {
                return ZoomMath.Actual;
            }

            return stored.Zoom;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return ZoomMath.Actual;
        }
    }

    /// <summary>
    /// Writes <paramref name="factor"/> to <paramref name="path"/>, creating the folder when needed.
    /// </summary>
    /// <param name="path">JSON file to write.</param>
    /// <param name="factor">Zoom factor. Must be within <see cref="ZoomMath"/> limits.</param>
    public static void Save(string path, double factor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsAllowed(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(new StoredZoom { Zoom = factor }, JsonOptions));
    }

    private static bool IsAllowed(double factor) =>
        !double.IsNaN(factor)
        && !double.IsInfinity(factor)
        && factor >= ZoomMath.Minimum
        && factor <= ZoomMath.Maximum;

    private sealed class StoredZoom
    {
        public double Zoom { get; set; }
    }
}
