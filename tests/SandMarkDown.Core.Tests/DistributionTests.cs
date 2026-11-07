using System.Text.Json.Nodes;
using SandMarkDown.Core;
using SandMarkDown.Distribution;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the GitHub distribution identity, version rules, and release workflow boundaries.
/// </summary>
public class DistributionTests
{
    /// <summary>
    /// The catalog, project version, Velopack pin, and support slug describe one product.
    /// The catalog does not carry a second version.
    /// </summary>
    [Fact]
    public void Catalog_matches_the_application_and_has_no_product_version()
    {
        var root = RepositoryRoot();
        var catalog = DistributionCatalog.Load(Path.Combine(root, "distribution", "catalog.json"));

        Assert.Equal(Branding.ProductName, catalog.Name);
        Assert.Equal(Branding.Description, catalog.Description);
        Assert.Equal("marksn-ms/SandMarkDown", catalog.Repository);
        Assert.Equal(Branding.SourceUrl, "https://github.com/" + catalog.Repository);
        Assert.Equal("sandmarkdown", catalog.SupportalSlug);
        Assert.Contains($"slug={catalog.SupportalSlug}", Branding.SupportUrl, StringComparison.Ordinal);
        Assert.Equal("win", catalog.Channel);
        Assert.Equal("win-x64", catalog.Runtime);
        Assert.Equal("SandMarkDown", catalog.PackId);
        Assert.Equal("releases.win.json", catalog.FeedAsset);
        Assert.Equal("SandMarkDown-win-Setup.exe", catalog.InstallerAsset);
        Assert.Equal("SandMarkDown-win-Portable.zip", catalog.PortableAsset);
        Assert.Equal("SHA256SUMS.txt", catalog.ChecksumAsset);
        Assert.Contains("{version}", catalog.FullPackagePattern, StringComparison.Ordinal);
        Assert.StartsWith(catalog.PackId + "-", catalog.FullPackagePattern, StringComparison.Ordinal);

        var project = Path.Combine(root, catalog.Project.Replace('/', Path.DirectorySeparatorChar));
        var version = ReleasePlanner.ReadProjectVersion(project);
        Assert.True(ReleasePlanner.TryParseStable(version, out _));

        var appProject = File.ReadAllText(project);
        Assert.Contains($"Include=\"Velopack\" Version=\"{catalog.VelopackVersion}\"", appProject, StringComparison.Ordinal);
        var tools = File.ReadAllText(Path.Combine(root, "dotnet-tools.json"));
        Assert.Contains(catalog.VelopackVersion, tools, StringComparison.Ordinal);

        var local = Path.Combine(@"C:\Users\reader\AppData\Local");
        var data = AppStorage.DataFolder(local);
        var install = AppStorage.InstallFolder(local, catalog.PackId);
        Assert.False(data.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.False(install.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A push publishes a higher version when the tag is free, and refuses to overwrite or downgrade.
    /// </summary>
    [Theory]
    [InlineData(null, "1.0.0", false, ReleaseAction.Skip)]
    [InlineData("1.0.0", "1.0.0", false, ReleaseAction.Skip)]
    [InlineData("1.0.0", "1.0.1", false, ReleaseAction.Publish)]
    [InlineData("1.0.0", "1.0.1", true, ReleaseAction.Reject)]
    [InlineData("1.0.1", "1.0.0", false, ReleaseAction.Reject)]
    [InlineData("1.0.0", "1.0", false, ReleaseAction.Reject)]
    [InlineData("1.0.0", "01.0.1", false, ReleaseAction.Reject)]
    public void Push_publishes_only_a_new_higher_version(string? previous, string current, bool tagExists, ReleaseAction expected)
    {
        var decision = ReleasePlanner.ForPush(previous, current, tagExists);

        Assert.Equal(expected, decision.Action);
        if (expected == ReleaseAction.Publish)
        {
            Assert.Equal("v" + current, decision.Tag);
            Assert.Equal(previous, decision.PreviousVersion);
        }
    }

    /// <summary>
    /// A manual run publishes the current version once, and a second run leaves the existing tag alone.
    /// </summary>
    [Theory]
    [InlineData("1.0.0", false, ReleaseAction.Publish)]
    [InlineData("1.0.0", true, ReleaseAction.Skip)]
    [InlineData("1.0.0.0", false, ReleaseAction.Reject)]
    public void Manual_run_does_not_overwrite_a_tag(string current, bool tagExists, ReleaseAction expected)
    {
        Assert.Equal(expected, ReleasePlanner.ForManual(current, tagExists).Action);
    }

    /// <summary>
    /// The uploaded feed lists only packages that are in the upload directory.
    /// </summary>
    [Fact]
    public void Feed_keeps_only_files_that_will_be_uploaded()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "sandmarkdown-feed-" + Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "SandMarkDown-1.0.1-full.nupkg"), "full");
            var feedPath = Path.Combine(directory.FullName, "releases.win.json");
            File.WriteAllText(
                feedPath,
                """
                {
                  "Assets": [
                    { "FileName": "SandMarkDown-1.0.0-full.nupkg", "Type": "Full" },
                    { "FileName": "SandMarkDown-1.0.1-full.nupkg", "Type": "Full" },
                    { "FileName": "SandMarkDown-1.0.1-delta.nupkg", "Type": "Delta" }
                  ]
                }
                """);

            var removed = ReleasePlanner.FilterFeed(feedPath, directory.FullName);

            Assert.Equal(2, removed);
            var feed = JsonNode.Parse(File.ReadAllText(feedPath))!;
            var names = feed["Assets"]!.AsArray().Select(asset => asset!["FileName"]!.GetValue<string>()).ToList();
            Assert.Equal(["SandMarkDown-1.0.1-full.nupkg"], names);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Pull requests build and test. Only a version change on main can publish, and it cannot replace a tag.
    /// </summary>
    [Fact]
    public void Workflows_publish_only_after_a_successful_main_build()
    {
        var root = RepositoryRoot();
        var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Assert.Contains("pull_request:", ci, StringComparison.Ordinal);
        Assert.Contains("contents: read", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("vpk", ci, StringComparison.Ordinal);

        Assert.Contains("branches: [main]", release, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", release, StringComparison.Ordinal);
        Assert.Contains("src/SandMarkDown/SandMarkDown.csproj", release, StringComparison.Ordinal);
        Assert.Contains("contents: read", release, StringComparison.Ordinal);
        Assert.Contains("contents: write", release, StringComparison.Ordinal);
        Assert.Contains("id-token: write", release, StringComparison.Ordinal);
        Assert.Contains("attestations: write", release, StringComparison.Ordinal);
        Assert.Contains("artifact-metadata: write", release, StringComparison.Ordinal);
        Assert.Contains("actions/attest-build-provenance@v4", release, StringComparison.Ordinal);
        Assert.Contains("does not overwrite", release, StringComparison.Ordinal);
        Assert.DoesNotContain("--clobber", release, StringComparison.Ordinal);
        Assert.DoesNotContain("--force", release, StringComparison.Ordinal);
        Assert.True(
            release.IndexOf("dotnet test", StringComparison.Ordinal) < release.IndexOf("gh release create", StringComparison.Ordinal));
        Assert.True(
            release.IndexOf("attest-build-provenance", StringComparison.Ordinal) < release.IndexOf("gh release create", StringComparison.Ordinal));
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
