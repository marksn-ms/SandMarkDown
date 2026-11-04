namespace SandMarkDown.Core;

/// <summary>
/// A stop in the reader's back and forward list.
/// </summary>
/// <param name="Path">Full path of a Markdown file. Null is the welcome page.</param>
/// <param name="Fragment">In-page fragment without a leading #. Null when the stop has no fragment.</param>
public readonly record struct HistoryPlace(string? Path, string? Fragment);

/// <summary>
/// Remembers Markdown files and the welcome page so the reader can move backward and forward.
/// </summary>
public sealed class ReadingHistory
{
    private readonly List<HistoryPlace> _places = [];
    private int _index = -1;

    /// <summary>
    /// True when a stop exists before the current one.
    /// </summary>
    public bool CanGoBack => _index > 0;

    /// <summary>
    /// True when a stop exists after the current one.
    /// </summary>
    public bool CanGoForward => _index >= 0 && _index < _places.Count - 1;

    /// <summary>
    /// The stop currently shown. Null before anything is recorded.
    /// </summary>
    public HistoryPlace? Current => _index >= 0 && _index < _places.Count ? _places[_index] : null;

    /// <summary>
    /// Appends a stop and drops any forward stops.
    /// Recording the same path and fragment as the current stop leaves the list unchanged.
    /// </summary>
    /// <param name="path">Full path of a Markdown file, or null for the welcome page.</param>
    /// <param name="fragment">In-page fragment. A leading # is ignored. Null when there is no fragment.</param>
    public void Record(string? path, string? fragment)
    {
        var place = new HistoryPlace(NormalizePath(path), NormalizeFragment(fragment));
        if (_index >= 0 && Same(_places[_index], place))
        {
            return;
        }

        if (_index >= 0 && _index < _places.Count - 1)
        {
            _places.RemoveRange(_index + 1, _places.Count - _index - 1);
        }

        _places.Add(place);
        _index = _places.Count - 1;
    }

    /// <summary>
    /// Moves to the previous stop.
    /// </summary>
    /// <returns>The stop now current, or null when there is nowhere to go.</returns>
    public HistoryPlace? GoBack()
    {
        if (!CanGoBack)
        {
            return null;
        }

        _index--;
        return _places[_index];
    }

    /// <summary>
    /// Moves to the next stop.
    /// </summary>
    /// <returns>The stop now current, or null when there is nowhere to go.</returns>
    public HistoryPlace? GoForward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        _index++;
        return _places[_index];
    }

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private static string? NormalizeFragment(string? fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return null;
        }

        var trimmed = fragment.TrimStart('#');
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool Same(HistoryPlace left, HistoryPlace right) =>
        string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Fragment, right.Fragment, StringComparison.Ordinal);
}
