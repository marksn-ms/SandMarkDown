using System.Text;

namespace SandMarkDown.Core;

/// <summary>
/// Writes unexpected errors to a local text file. Nothing is sent anywhere.
/// </summary>
public static class ErrorLog
{
    /// <summary>
    /// Largest log size before the current file is kept as <c>error.old.log</c> and a new one starts.
    /// </summary>
    public const long MaxBytes = 512 * 1024;

    /// <summary>
    /// Appends <paramref name="exception"/> to <c>error.log</c> in <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Folder for the log. It is created when missing.</param>
    /// <param name="exception">The error to record.</param>
    /// <param name="when">Time of the error.</param>
    /// <param name="version">Product version shown in the entry.</param>
    /// <returns>Full path of the log file.</returns>
    public static string Write(string directory, Exception exception, DateTimeOffset when, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(exception);

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "error.log");
        var info = new FileInfo(path);
        if (info.Exists && info.Length > MaxBytes)
        {
            File.Move(path, Path.Combine(directory, "error.old.log"), overwrite: true);
        }

        File.AppendAllText(path, Format(exception, when, version), Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// Formats one log entry.
    /// </summary>
    /// <param name="exception">The error to record.</param>
    /// <param name="when">Time of the error.</param>
    /// <param name="version">Product version shown in the entry.</param>
    /// <returns>Entry text ending with a blank line.</returns>
    public static string Format(Exception exception, DateTimeOffset when, string version)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var builder = new StringBuilder();
        builder.Append(when.ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("  ");
        builder.Append(Branding.ProductName);
        builder.Append(' ');
        builder.AppendLine(version);
        builder.AppendLine(exception.ToString());
        builder.AppendLine();
        return builder.ToString();
    }
}
