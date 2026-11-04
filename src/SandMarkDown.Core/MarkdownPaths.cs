namespace SandMarkDown.Core;

/// <summary>
/// Recognizes Markdown file names the reader can open.
/// </summary>
public static class MarkdownPaths
{
    /// <summary>
    /// Returns whether <paramref name="path"/> uses a Markdown extension.
    /// </summary>
    /// <param name="path">File path or name.</param>
    /// <returns><see langword="true"/> for <c>.md</c> and <c>.markdown</c>.</returns>
    public static bool IsMarkdownFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }
}
