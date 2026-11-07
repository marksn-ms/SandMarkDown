using System.Text.Json;

namespace SandMarkDown.Distribution;

/// <summary>
/// Identity of the SandMarkDown GitHub distribution. The product version is not stored here.
/// It lives in the application project file.
/// </summary>
/// <param name="Name">Product name.</param>
/// <param name="Description">One-line description.</param>
/// <param name="Repository">GitHub repository as <c>owner/name</c>.</param>
/// <param name="Project">Application project path from the repository root.</param>
/// <param name="PackId">Velopack pack id and install folder name.</param>
/// <param name="Channel">Velopack channel. <c>win</c> is the default Windows channel.</param>
/// <param name="Runtime">Runtime identifier, such as <c>win-x64</c>.</param>
/// <param name="VelopackVersion">Pinned Velopack package and <c>vpk</c> tool version.</param>
/// <param name="SupportalSlug">Supportal product slug.</param>
/// <param name="InstallerAsset">Setup file name on the GitHub Release.</param>
/// <param name="PortableAsset">Portable zip file name.</param>
/// <param name="ChecksumAsset">SHA-256 checksum file name.</param>
/// <param name="FeedAsset">Velopack feed file name.</param>
/// <param name="FullPackagePattern">Full package file name with a <c>{version}</c> placeholder.</param>
/// <param name="DeltaPackagePattern">Delta package file name with a <c>{version}</c> placeholder.</param>
public sealed record DistributionCatalog(
    string Name,
    string Description,
    string Repository,
    string Project,
    string PackId,
    string Channel,
    string Runtime,
    string VelopackVersion,
    string SupportalSlug,
    string InstallerAsset,
    string PortableAsset,
    string ChecksumAsset,
    string FeedAsset,
    string FullPackagePattern,
    string DeltaPackagePattern)
{
    /// <summary>
    /// Reads <paramref name="path"/>. The catalog must not carry a product version.
    /// </summary>
    /// <param name="path">Path to <c>distribution/catalog.json</c>.</param>
    /// <returns>The catalog.</returns>
    public static DistributionCatalog Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.TryGetProperty("version", out _))
        {
            throw new InvalidOperationException("distribution/catalog.json must not contain a product version. The version is the <Version> in the application project.");
        }

        return new DistributionCatalog(
            Required(root, "name"),
            Required(root, "description"),
            Required(root, "repository"),
            Required(root, "project"),
            Required(root, "packId"),
            Required(root, "channel"),
            Required(root, "runtime"),
            Required(root, "velopackVersion"),
            Required(root, "supportalSlug"),
            Required(root, "installerAsset"),
            Required(root, "portableAsset"),
            Required(root, "checksumAsset"),
            Required(root, "feedAsset"),
            Required(root, "fullPackagePattern"),
            Required(root, "deltaPackagePattern"));
    }

    private static string Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"distribution/catalog.json is missing '{name}'.");
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"distribution/catalog.json '{name}' is empty.");
        }

        return text;
    }
}
