using System.Text.Json;

namespace SandMarkDown.Core;

/// <summary>
/// View menu choices, remembered between sessions.
/// </summary>
/// <param name="ShowPreview">Show the miniature document on the right edge.</param>
/// <param name="ShowSidebar">Show the heading list beside the page.</param>
/// <param name="DarkMode">Use the dark Sandmark palette. Light is used when off.</param>
public sealed record ViewSettings(bool ShowPreview = true, bool ShowSidebar = true, bool DarkMode = true)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Defaults: preview and sidebar shown, dark mode on.
    /// </summary>
    public static ViewSettings Default { get; } = new();

    /// <summary>
    /// Reads settings from <paramref name="path"/>. Missing or unreadable files give <see cref="Default"/>.
    /// </summary>
    /// <param name="path">JSON file written by <see cref="Save"/>.</param>
    /// <returns>The saved settings, or the defaults.</returns>
    public static ViewSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<ViewSettings>(File.ReadAllText(path), JsonOptions) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Default;
        }
    }

    /// <summary>
    /// Writes these settings to <paramref name="path"/>, creating the folder when needed.
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
    /// Value for the page's <c>data-theme</c> attribute.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Theme => DarkMode ? "dark" : "light";

    /// <summary>
    /// JSON the page script reads to apply these settings without reloading.
    /// </summary>
    /// <returns>An object with <c>preview</c>, <c>sidebar</c>, and <c>dark</c> flags.</returns>
    public string ToPageJson() =>
        JsonSerializer.Serialize(new { preview = ShowPreview, sidebar = ShowSidebar, dark = DarkMode });
}
