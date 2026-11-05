using System.Text.RegularExpressions;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the Microsoft Store package manifest and its assets.
/// </summary>
public class PackagingTests
{
    /// <summary>
    /// The package opens Markdown files, runs as a full-trust desktop app, and keeps identity as placeholders
    /// for the build script to fill.
    /// </summary>
    [Fact]
    public void Manifest_declares_markdown_files_and_identity_placeholders()
    {
        var manifest = File.ReadAllText(Path.Combine(RepositoryRoot(), "packaging", "AppxManifest.xml"));

        Assert.Contains("<uap:FileType>.md</uap:FileType>", manifest, StringComparison.Ordinal);
        Assert.Contains("<uap:FileType>.markdown</uap:FileType>", manifest, StringComparison.Ordinal);
        Assert.Contains("Executable=\"SandMarkDown.exe\"", manifest, StringComparison.Ordinal);
        Assert.Contains("runFullTrust", manifest, StringComparison.Ordinal);
        foreach (var token in new[] { "@@IDENTITY_NAME@@", "@@PUBLISHER@@", "@@PUBLISHER_DISPLAY_NAME@@", "@@VERSION@@", "@@ARCH@@" })
        {
            Assert.Contains(token, manifest, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every asset the manifest names exists.
    /// </summary>
    [Fact]
    public void Manifest_assets_exist()
    {
        var root = Path.Combine(RepositoryRoot(), "packaging");
        var manifest = File.ReadAllText(Path.Combine(root, "AppxManifest.xml"));
        var assets = Regex.Matches(manifest, @"Assets\\[A-Za-z0-9._-]+\.png").Select(m => m.Value).Distinct().ToList();

        Assert.NotEmpty(assets);
        Assert.All(assets, asset => Assert.True(File.Exists(Path.Combine(root, asset)), asset));
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Square44x44Logo.targetsize-256.png")));
    }

    /// <summary>
    /// The publisher in StoreIdentity.json hashes to the package family name Partner Center assigned,
    /// so a typo in the publisher fails here instead of at upload.
    /// </summary>
    [Fact]
    public void Store_identity_matches_the_package_family_name()
    {
        var path = Path.Combine(RepositoryRoot(), "packaging", "StoreIdentity.json");
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        var name = root.GetProperty("identityName").GetString()!;
        var publisher = root.GetProperty("publisher").GetString()!;
        var family = root.GetProperty("packageFamilyName").GetString()!;

        Assert.Equal(family, $"{name}_{PublisherId(publisher)}");
        Assert.Equal("Sandmark Technologies, LLC", root.GetProperty("publisherDisplayName").GetString());
    }

    // Windows' publisher ID: the first 8 bytes of SHA-256 over the UTF-16 publisher, in 5-bit groups.
    private static string PublisherId(string publisher)
    {
        const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(publisher));
        var bits = string.Concat(hash.Take(8).Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))) + "0";
        var id = new System.Text.StringBuilder();
        for (var i = 0; i < bits.Length; i += 5)
        {
            id.Append(alphabet[Convert.ToInt32(bits.Substring(i, 5), 2)]);
        }

        return id.ToString();
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SandMarkDown.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
