namespace SandMarkDown.Core;

/// <summary>
/// One registry value to write under HKEY_CURRENT_USER.
/// </summary>
/// <param name="SubKey">Key path below HKEY_CURRENT_USER.</param>
/// <param name="Name">Value name. An empty name is the key's default value.</param>
/// <param name="Value">String value. <see langword="null"/> writes an empty marker value with no data.</param>
public sealed record RegistryEntry(string SubKey, string Name, string? Value);

/// <summary>
/// Per-user registration that lets Windows offer SandMarkDown for Markdown files, in Open with and in
/// Settings, Default apps. It never sets the default itself. Windows reserves that choice for the user.
/// </summary>
public static class AssociationPlan
{
    /// <summary>
    /// File type ID shared with the installer script.
    /// </summary>
    public const string ProgId = "SandMarkDown.md";

    /// <summary>
    /// Name under RegisteredApplications. Settings uses it to open SandMarkDown's Default apps page.
    /// </summary>
    public const string RegisteredName = "SandMarkDown";

    /// <summary>
    /// Key holding SandMarkDown's capabilities.
    /// </summary>
    public const string CapabilitiesKey = @"Software\Sandmark Technologies\SandMarkDown\Capabilities";

    /// <summary>
    /// Extensions SandMarkDown registers for.
    /// </summary>
    public static IReadOnlyList<string> Extensions { get; } = [".md", ".markdown"];

    /// <summary>
    /// Settings page for SandMarkDown in Default apps, for a copy registered by this plan.
    /// </summary>
    public const string SettingsUri = "ms-settings:defaultapps?registeredAppUser=" + RegisteredName;

    /// <summary>
    /// Builds the registry values that point Markdown files at <paramref name="executablePath"/>.
    /// </summary>
    /// <param name="executablePath">Full path of SandMarkDown.exe.</param>
    /// <returns>Values to write, in order.</returns>
    public static IReadOnlyList<RegistryEntry> For(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var exe = Path.GetFullPath(executablePath);
        var command = $"\"{exe}\" \"%1\"";
        var entries = new List<RegistryEntry>
        {
            new($@"Software\Classes\{ProgId}", string.Empty, "Markdown Document"),
            new($@"Software\Classes\{ProgId}\DefaultIcon", string.Empty, $"\"{exe}\",0"),
            new($@"Software\Classes\{ProgId}\shell\open\command", string.Empty, command),
            new(@"Software\Classes\Applications\SandMarkDown.exe", "FriendlyAppName", Branding.ProductName),
            new(@"Software\Classes\Applications\SandMarkDown.exe\shell\open\command", string.Empty, command),
            new(@"Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe", string.Empty, exe),
            new(CapabilitiesKey, "ApplicationName", Branding.ProductName),
            new(CapabilitiesKey, "ApplicationDescription", Branding.Description),
            new(CapabilitiesKey, "ApplicationIcon", $"\"{exe}\",0"),
            new(@"Software\RegisteredApplications", RegisteredName, CapabilitiesKey),
        };

        foreach (var extension in Extensions)
        {
            entries.Add(new($@"Software\Classes\{extension}\OpenWithProgids", ProgId, null));
            entries.Add(new(@"Software\Classes\Applications\SandMarkDown.exe\SupportedTypes", extension, string.Empty));
            entries.Add(new($@"{CapabilitiesKey}\FileAssociations", extension, ProgId));
        }

        return entries;
    }

    /// <summary>
    /// Reads the program path from an open command such as <c>"C:\apps\SandMarkDown.exe" "%1"</c>.
    /// </summary>
    /// <param name="command">Command text from the registry.</param>
    /// <returns>The program path, or <see langword="null"/> when the command is empty or malformed.</returns>
    public static string? ExecutableFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = command.Trim();
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }
}
