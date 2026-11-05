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
