using System.Text;

namespace SandMarkDown.Core;

/// <summary>
/// Version details a user can paste into a support request.
/// </summary>
/// <param name="Version">Product version, such as <c>1.0.0</c>.</param>
/// <param name="Commit">Short source commit, or <see langword="null"/> when unknown.</param>
/// <param name="Windows">Windows version description.</param>
/// <param name="DotNet">.NET runtime description.</param>
/// <param name="WebView2">WebView2 runtime version, or <see langword="null"/> when unavailable.</param>
public sealed record DiagnosticReport(string Version, string? Commit, string Windows, string DotNet, string? WebView2)
{
    /// <summary>
    /// Version text for the About box, such as <c>1.0.0 (a1b2c3d)</c>.
    /// </summary>
    public string DisplayVersion => Commit is null ? Version : $"{Version} ({Commit})";

    /// <summary>
    /// Reads the version and short commit from an informational version such as <c>1.0.0+a1b2c3d4e5</c>.
    /// </summary>
    /// <param name="informationalVersion">Assembly informational version.</param>
    /// <param name="fallback">Version used when <paramref name="informationalVersion"/> is empty.</param>
    /// <returns>The version and a seven-character commit, or no commit.</returns>
    public static (string Version, string? Commit) ParseInformationalVersion(string? informationalVersion, string fallback)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return (fallback, null);
        }

        var plus = informationalVersion.IndexOf('+');
        if (plus < 0)
        {
            return (informationalVersion, null);
        }

        var commit = informationalVersion[(plus + 1)..];
        return (informationalVersion[..plus], commit.Length > 7 ? commit[..7] : commit);
    }

    /// <summary>
    /// Plain text for the clipboard.
    /// </summary>
    /// <returns>One detail per line.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{Branding.ProductName} {DisplayVersion}");
        builder.AppendLine($"Windows: {Windows}");
        builder.AppendLine($".NET: {DotNet}");
        builder.AppendLine($"WebView2: {WebView2 ?? "not available"}");
        return builder.ToString();
    }
}
