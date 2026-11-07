namespace SandMarkDown.Core;

/// <summary>
/// Folders for SandMarkDown settings and for the Velopack install.
/// Settings stay outside the install folder so an upgrade or uninstall can replace the program without deleting them.
/// </summary>
public static class AppStorage
{
    /// <summary>
    /// Company folder under LocalAppData. Reading settings live here, not in the install folder.
    /// </summary>
    public const string CompanyFolder = "Sandmark Technologies";

    /// <summary>
    /// Product folder under <see cref="CompanyFolder"/>.
    /// </summary>
    public const string ProductFolder = "SandMarkDown";

    /// <summary>
    /// Settings, recent files, the WebView2 profile, and the error log.
    /// </summary>
    /// <param name="localAppData">The LocalAppData folder.</param>
    /// <returns>The SandMarkDown data folder.</returns>
    public static string DataFolder(string localAppData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        return Path.Combine(localAppData, CompanyFolder, ProductFolder);
    }

    /// <summary>
    /// Velopack's per-user install folder for a pack id. Uninstall deletes this folder.
    /// </summary>
    /// <param name="localAppData">The LocalAppData folder.</param>
    /// <param name="packId">Velopack pack id.</param>
    /// <returns>The install folder.</returns>
    public static string InstallFolder(string localAppData, string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return Path.Combine(localAppData, packId);
    }
}
