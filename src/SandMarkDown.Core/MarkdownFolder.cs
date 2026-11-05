namespace SandMarkDown.Core;

/// <summary>
/// One row in the Explorer sidebar.
/// </summary>
/// <param name="Name">File or folder name.</param>
/// <param name="FullPath">Full path.</param>
/// <param name="IsFolder">True for a folder.</param>
public sealed record FolderEntry(string Name, string FullPath, bool IsFolder);

/// <summary>
/// Lists what the Explorer sidebar shows for a folder: subfolders, then Markdown files.
/// </summary>
public static class MarkdownFolder
{
    // Folders that hold tooling or build output rather than documents.
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "packages", "artifacts", "TestResults"
    };

    /// <summary>
    /// Lists subfolders and Markdown files in <paramref name="folder"/>, each group sorted by name.
    /// Hidden folders, dot folders, and build folders are left out.
    /// </summary>
    /// <param name="folder">Folder to list.</param>
    /// <returns>The entries, or an empty list when the folder cannot be read.</returns>
    public static IReadOnlyList<FolderEntry> List(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        try
        {
            var directory = new DirectoryInfo(folder);
            if (!directory.Exists)
            {
                return [];
            }

            var folders = directory.EnumerateDirectories()
                .Where(d => !d.Name.StartsWith('.')
                    && !Skipped.Contains(d.Name)
                    && (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(d => new FolderEntry(d.Name, d.FullName, IsFolder: true));

            var files = directory.EnumerateFiles()
                .Where(f => MarkdownPaths.IsMarkdownFile(f.Name)
                    && (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(f => new FolderEntry(f.Name, f.FullName, IsFolder: false));

            return folders.Concat(files).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    /// <summary>
    /// Returns the folders from <paramref name="root"/> down to the folder holding <paramref name="file"/>,
    /// so the sidebar can expand to it.
    /// </summary>
    /// <param name="root">Folder shown at the top of the sidebar.</param>
    /// <param name="file">File to reveal.</param>
    /// <returns>Each folder below the root on the way to the file, or <see langword="null"/> when the file is outside the root.</returns>
    public static IReadOnlyList<string>? PathFromRoot(string root, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullFile = Path.GetFullPath(file);
        var relative = Path.GetRelativePath(fullRoot, fullFile);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        var folders = new List<string>();
        var current = fullRoot;
        var parts = relative.Split(Path.DirectorySeparatorChar);
        foreach (var part in parts[..^1])
        {
            current = Path.Combine(current, part);
            folders.Add(current);
        }

        return folders;
    }
}
