namespace SandMarkDown.Core;

/// <summary>
/// Recognizes Markdown file names the reader can open, and the local files a page may load.
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

    /// <summary>
    /// Returns the MIME type for an image file the page may display.
    /// </summary>
    /// <param name="path">File path or name.</param>
    /// <returns>An image MIME type, or <see langword="null"/> when the file is not a supported image.</returns>
    public static string? ImageContentType(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            _ => null
        };
    }

    /// <summary>
    /// Returns whether <paramref name="resourcePath"/> is on this computer, or on the same network share
    /// server as <paramref name="documentPath"/>.
    /// </summary>
    /// <param name="documentPath">Full path of the Markdown file being read.</param>
    /// <param name="resourcePath">Full path of a file the page refers to.</param>
    /// <returns><see langword="false"/> when loading the file would contact another computer.</returns>
    public static bool IsSameMachineOrShare(string documentPath, string resourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);

        var resourceHost = NetworkHost(resourcePath);
        if (resourceHost is null)
        {
            return true;
        }

        var documentHost = NetworkHost(documentPath);
        return documentHost is not null
            && string.Equals(documentHost, resourceHost, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NetworkHost(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return FirstSegment(path[@"\\?\UNC\".Length..]);
        }

        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            var rest = path[4..];
            // \\?\C:\ is a local drive written in long-path form. Any other device path is treated as remote.
            return rest.Length >= 2 && rest[1] == ':' ? null : rest;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return FirstSegment(path[2..]);
        }

        return null;
    }

    private static string FirstSegment(string rest)
    {
        var end = rest.IndexOfAny(['\\', '/']);
        return end < 0 ? rest : rest[..end];
    }
}
