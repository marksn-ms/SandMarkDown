namespace SandMarkDown.Core;

/// <summary>
/// Command-line arguments for a reader launch. Velopack hook arguments are not files.
/// </summary>
public static class LaunchArguments
{
    /// <summary>
    /// The first argument that names a file, or <see langword="null"/> when the launch has no file.
    /// Help flags and Velopack hook flags are skipped.
    /// </summary>
    /// <param name="args">Arguments after the program path.</param>
    /// <returns>The file path argument, or <see langword="null"/>.</returns>
    public static string? FilePath(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var skipHookValue = false;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (skipHookValue)
            {
                // Velopack passes the version after the hook name: --veloapp-install 1.0.0
                skipHookValue = false;
                continue;
            }

            if (arg is "-h" or "--help" or "/?")
            {
                continue;
            }

            if (arg.StartsWith("--velo", StringComparison.OrdinalIgnoreCase))
            {
                skipHookValue = true;
                continue;
            }

            return arg;
        }

        return null;
    }

    /// <summary>
    /// True when startup may ask about an update. A file on the command line is opened instead,
    /// so double-clicking a Markdown file is not interrupted by the prompt.
    /// </summary>
    /// <param name="args">Arguments after the program path.</param>
    /// <returns>True when no file was passed.</returns>
    public static bool ShouldCheckForUpdates(IReadOnlyList<string> args) => FilePath(args) is null;
}
