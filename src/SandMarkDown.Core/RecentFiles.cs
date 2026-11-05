using System.Text.Json;

namespace SandMarkDown.Core;

/// <summary>
/// Most recently opened Markdown files, newest first, for File, Open Recent.
/// </summary>
public sealed class RecentFiles
{
    /// <summary>
    /// Most files kept.
    /// </summary>
    public const int Capacity = 10;

    private readonly List<string> _items = [];

    /// <summary>
    /// Full paths, newest first.
    /// </summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>
    /// Moves <paramref name="path"/> to the top, adding it when new and dropping the oldest past <see cref="Capacity"/>.
    /// </summary>
    /// <param name="path">File that was opened.</param>
    public void Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        _items.RemoveAll(item => string.Equals(item, full, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, full);
        if (_items.Count > Capacity)
        {
            _items.RemoveRange(Capacity, _items.Count - Capacity);
        }
    }

    /// <summary>
    /// Removes <paramref name="path"/> from the list.
    /// </summary>
    /// <param name="path">File to forget.</param>
    /// <returns><see langword="true"/> when it was in the list.</returns>
    public bool Remove(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        return _items.RemoveAll(item => string.Equals(item, full, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>
    /// Empties the list.
    /// </summary>
    public void Clear() => _items.Clear();

    /// <summary>
    /// Reads the list from <paramref name="path"/>. A missing or unreadable file gives an empty list.
    /// </summary>
    /// <param name="path">JSON file written by <see cref="Save"/>.</param>
    /// <returns>The saved list.</returns>
    public static RecentFiles Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var recent = new RecentFiles();
        if (!File.Exists(path))
        {
            return recent;
        }

        try
        {
            var items = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
            foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item)).Reverse())
            {
                recent.Add(item);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            recent.Clear();
        }

        return recent;
    }

    /// <summary>
    /// Writes the list to <paramref name="path"/>, creating the folder when needed.
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

        File.WriteAllText(path, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Menu text for an entry: an access-key number and the file name, with the folder after it.
    /// </summary>
    /// <param name="index">Zero-based position in <see cref="Items"/>.</param>
    /// <returns>Text such as <c>_1  notes.md  (C:\docs)</c>.</returns>
    public string MenuText(int index)
    {
        var path = _items[index];
        var number = index < 9 ? $"_{index + 1}" : "1_0";
        var name = Path.GetFileName(path).Replace("_", "__", StringComparison.Ordinal);
        var folder = Path.GetDirectoryName(path)?.Replace("_", "__", StringComparison.Ordinal);
        return $"{number}  {name}  ({folder})";
    }
}
