using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies Sandmark Technologies brand constants and packaged assets.
/// </summary>
public class BrandingTests
{
    /// <summary>
    /// Product identity matches the Sandmark About box.
    /// </summary>
    [Fact]
    public void Branding_constants_match_sandmark_identity()
    {
        Assert.Equal("SandMarkDown", Branding.ProductName);
        Assert.Equal("Sandmark Technologies", Branding.CompanyName);
        Assert.Equal("https://sandmarktech.com", Branding.WebsiteUrl);
        Assert.Contains("Sandmark Technologies", Branding.ProductTitleSuffix, StringComparison.Ordinal);
        Assert.Contains("Sandmark Technologies", Branding.Copyright, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(Branding.Description));
        Assert.StartsWith(Branding.WebsiteUrl, Branding.PrivacyUrl, StringComparison.Ordinal);
        Assert.StartsWith(Branding.WebsiteUrl, Branding.SupportUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// The license and third-party notices ship with the app.
    /// </summary>
    [Fact]
    public void License_and_notices_ship_with_the_app()
    {
        var root = RepositoryRoot();
        Assert.Contains("MIT License", File.ReadAllText(Path.Combine(root, "LICENSE")), StringComparison.Ordinal);
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));
        foreach (var (name, _, _) in AboutPage.Credits)
        {
            Assert.Contains(name, notices, StringComparison.Ordinal);
        }

        var csproj = File.ReadAllText(Path.Combine(root, "src", "SandMarkDown", "SandMarkDown.csproj"));
        Assert.Contains("THIRD-PARTY-NOTICES.txt", csproj, StringComparison.Ordinal);
    }

    /// <summary>
    /// The repository keeps the dark Sandmark token reference.
    /// </summary>
    [Fact]
    public void Repository_keeps_sandmark_token_reference()
    {
        var tokens = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "tokens.css"));
        Assert.Contains("--smt-page: #101418", tokens, StringComparison.Ordinal);
        Assert.Contains("--smt-accent: #8ec0e0", tokens, StringComparison.Ordinal);
        Assert.Contains("[data-theme=\"dark\"]", tokens, StringComparison.Ordinal);
    }

    /// <summary>
    /// The WPF project embeds the Sandmark logo and company metadata.
    /// </summary>
    [Fact]
    public void Wpf_project_embeds_sandmark_logo_assets()
    {
        var root = RepositoryRoot();
        var assets = Path.Combine(root, "src", "SandMarkDown", "Assets");
        Assert.True(File.Exists(Path.Combine(assets, "logo-small-narrow.jpg")));
        Assert.True(File.Exists(Path.Combine(assets, "logo-icon.jpg")));
        Assert.True(File.Exists(Path.Combine(assets, "favicon.ico")));

        var csproj = File.ReadAllText(Path.Combine(root, "src", "SandMarkDown", "SandMarkDown.csproj"));
        Assert.Contains("logo-small-narrow.jpg", csproj, StringComparison.Ordinal);
        Assert.Contains("ApplicationIcon", csproj, StringComparison.Ordinal);
        Assert.Contains("Sandmark Technologies", csproj, StringComparison.Ordinal);
        Assert.Contains("GenerateDocumentationFile", csproj, StringComparison.Ordinal);
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
