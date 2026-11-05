using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies Markdown becomes a dark reading page with safe local links.
/// </summary>
public class MarkdownHtmlRendererTests
{
    /// <summary>
    /// Headings, tables, task lists, and fenced code are present in the page.
    /// </summary>
    [Fact]
    public void Render_includes_common_reading_constructs()
    {
        var markdown = """
            # Hello

            A **bold** word and `code`.

            - [x] Done
            - [ ] Later

            | Col | Val |
            | --- | --- |
            | A   | 1   |

            ```
            line
            ```
            """;

        var rendered = MarkdownHtmlRenderer.Render(markdown, SamplePath());

        Assert.Contains("<h1", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("Hello", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("<strong>bold</strong>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"smt-table-scroll\"><table>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("<pre>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("data-theme=\"dark\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("--smt-page: #101418", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("--smt-accent: #8ec0e0", rendered.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// YAML front matter stays out of the reading view.
    /// </summary>
    [Fact]
    public void Render_hides_yaml_front_matter()
    {
        var markdown = """
            ---
            title: Hidden
            ---

            Visible body
            """;

        var rendered = MarkdownHtmlRenderer.Render(markdown, SamplePath());

        Assert.Contains("Visible body", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("title: Hidden", rendered.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An empty file still produces a page.
    /// </summary>
    [Fact]
    public void Render_explains_an_empty_file()
    {
        var rendered = MarkdownHtmlRenderer.Render("   \n", SamplePath());
        Assert.Contains("This file is empty.", rendered.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Relative images and Markdown links become viewer-local URLs.
    /// </summary>
    [Fact]
    public void Render_rewrites_relative_files_and_keeps_fragments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sandmarkdown-tests", Guid.NewGuid().ToString("N"));
        var notes = Path.Combine(directory, "notes");
        var document = Path.Combine(notes, "chapter.md");
        var rendered = MarkdownHtmlRenderer.Render(
            """
            ![diagram](../pic.png)
            [next](other.md#Next)
            """,
            document);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(notes, "..", "pic.png")),
            rendered.LocalResources.Values.Single(path => path.EndsWith("pic.png", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(notes, "other.md")),
            rendered.LocalResources.Values.Single(path => path.EndsWith("other.md", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains($"https://{MarkdownHtmlRenderer.VirtualHost}/local/", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("#Next", rendered.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Remote links stay remote, and script links are not left in place.
    /// </summary>
    [Fact]
    public void Render_leaves_web_links_and_drops_script_links()
    {
        var rendered = MarkdownHtmlRenderer.Render(
            """
            [site](https://sandmarktech.com)
            ![remote](https://example.com/a.png)
            [bad](javascript:alert(1))
            """,
            SamplePath());

        Assert.Contains("https://sandmarktech.com", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("https://example.com/a.png", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(rendered.LocalResources);
    }

    /// <summary>
    /// Script written in the file is left inert. Only the viewer's script carries the nonce the policy allows.
    /// </summary>
    [Fact]
    public void Render_allows_only_the_viewers_own_script()
    {
        var rendered = MarkdownHtmlRenderer.Render(
            """
            <script>document.title = "pwned"</script>

            ![pic](a.png){onerror="alert(1)"}
            """,
            SamplePath());

        var nonce = System.Text.RegularExpressions.Regex.Match(rendered.ContentSecurityPolicy, "'nonce-([^']+)'").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(nonce));
        Assert.Contains("default-src 'none'", rendered.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'none'", rendered.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline' 'nonce", rendered.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Matches("script-src 'nonce-[^']+';", rendered.ContentSecurityPolicy);
        Assert.Contains($"<script nonce=\"{nonce}\">", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("http-equiv=\"Content-Security-Policy\"", rendered.Html, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(rendered.Html, $"nonce=\"{nonce}\""));
    }

    /// <summary>
    /// Each page gets a fresh nonce.
    /// </summary>
    [Fact]
    public void Render_uses_a_new_nonce_each_time()
    {
        var first = MarkdownHtmlRenderer.Render("text", SamplePath());
        var second = MarkdownHtmlRenderer.Render("text", SamplePath());
        Assert.NotEqual(first.ContentSecurityPolicy, second.ContentSecurityPolicy);
    }

    /// <summary>
    /// Web images wait until the reader asks for them.
    /// </summary>
    [Fact]
    public void Render_hides_web_images_until_asked()
    {
        const string markdown = """
            ![one](https://example.com/a.png)
            ![two](//example.com/b.png)
            """;

        var hidden = MarkdownHtmlRenderer.Render(markdown, SamplePath());
        Assert.Equal(2, hidden.HiddenRemoteImages);
        Assert.Contains("data-smt-remote-src=\"https://example.com/a.png\"", hidden.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=\"https://example.com/a.png\"", hidden.Html.Replace("data-smt-remote-src", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("id=\"smt-show-remote\"", hidden.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("https: http:", hidden.ContentSecurityPolicy, StringComparison.Ordinal);

        var shown = MarkdownHtmlRenderer.Render(markdown, SamplePath(), showRemoteImages: true);
        Assert.Equal(0, shown.HiddenRemoteImages);
        Assert.Contains("src=\"https://example.com/a.png\"", shown.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"smt-show-remote\"", shown.Html, StringComparison.Ordinal);
        Assert.Contains("img-src https://sandmarkdown.local data: https: http:", shown.ContentSecurityPolicy, StringComparison.Ordinal);
    }

    /// <summary>
    /// An image on another computer's share is not loaded, because that would send sign-in details there.
    /// </summary>
    [Fact]
    public void Render_does_not_load_images_from_other_computers()
    {
        var rendered = MarkdownHtmlRenderer.Render(
            """
            ![unc](\\\\attacker\\share\\a.png)
            ![file](file://attacker/share/b.png)

            <img src="\\attacker\share\c.png">
            """,
            Path.Combine(@"C:\notes", "sample.md"));

        Assert.Empty(rendered.LocalResources);
        Assert.DoesNotContain("attacker", rendered.Html.Replace("alt=\"", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Placeholder text written in the file stays literal.
    /// </summary>
    [Fact]
    public void Render_keeps_template_placeholders_in_the_file_literal()
    {
        var rendered = MarkdownHtmlRenderer.Render(
            """
            # @@BODY@@

            ## @@NONCE@@

            @@OUTLINE@@ @@CSP@@
            """,
            SamplePath());

        Assert.Contains("@@NONCE@@", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("@@CSP@@", rendered.Html, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(rendered.Html, "<article"));
    }

    /// <summary>
    /// The welcome page names the product and uses the dark theme.
    /// </summary>
    [Fact]
    public void Welcome_uses_dark_sandmark_theme()
    {
        var welcome = MarkdownHtmlRenderer.Welcome();
        Assert.Null(welcome.DocumentPath);
        Assert.Empty(welcome.LocalResources);
        Assert.Contains(Branding.ProductName, welcome.Html, StringComparison.Ordinal);
        Assert.Contains("data-theme=\"dark\"", welcome.Html, StringComparison.Ordinal);
        Assert.Contains("chrome.webview.postMessage", welcome.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"smt-find\"", welcome.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("On this page", welcome.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two headings become an outline. A heading shown inside a code fence does not.
    /// </summary>
    [Fact]
    public void Render_lists_real_headings_in_the_outline()
    {
        var markdown = """
            # Alpha

            ```html
            <h2 id="fake">Fake</h2>
            ```

            ## Beta
            """;

        var rendered = MarkdownHtmlRenderer.Render(markdown, SamplePath());

        Assert.Contains("aria-label=\"On this page\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("href=\"#alpha\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("href=\"#beta\"", rendered.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"#fake\"", rendered.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A known fenced language is colored, and the source text remains.
    /// </summary>
    [Fact]
    public void Render_colors_fenced_csharp()
    {
        var markdown = """
            ```csharp
            public class A { }
            ```
            """;

        var rendered = MarkdownHtmlRenderer.Render(markdown, SamplePath());

        Assert.Contains("language-csharp", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("public", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("color:", rendered.Html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Markdown extensions are recognized.
    /// </summary>
    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("NOTES.MARKDOWN", true)]
    [InlineData("notes.txt", false)]
    public void Markdown_extension_check(string path, bool expected)
    {
        Assert.Equal(expected, MarkdownPaths.IsMarkdownFile(path));
    }

    /// <summary>
    /// Only image types are served to the page from disk.
    /// </summary>
    [Theory]
    [InlineData("a.PNG", "image/png")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("secret.txt", null)]
    [InlineData("id_ed25519", null)]
    [InlineData("notes.md", null)]
    public void Image_content_type_check(string path, string? expected)
    {
        Assert.Equal(expected, MarkdownPaths.ImageContentType(path));
    }

    /// <summary>
    /// Files on this computer, or on the same share as the document, may load. Other computers may not.
    /// </summary>
    [Theory]
    [InlineData(@"C:\notes\a.md", @"C:\notes\pic.png", true)]
    [InlineData(@"C:\notes\a.md", @"D:\pics\pic.png", true)]
    [InlineData(@"C:\notes\a.md", @"\\?\C:\notes\pic.png", true)]
    [InlineData(@"C:\notes\a.md", @"\\server\share\pic.png", false)]
    [InlineData(@"C:\notes\a.md", @"\\?\UNC\server\share\pic.png", false)]
    [InlineData(@"\\server\share\a.md", @"\\SERVER\share\pic.png", true)]
    [InlineData(@"\\server\share\a.md", @"\\other\share\pic.png", false)]
    public void Same_machine_or_share_check(string document, string resource, bool expected)
    {
        Assert.Equal(expected, MarkdownPaths.IsSameMachineOrShare(document, resource));
    }

    private static string SamplePath() => Path.Combine(Path.GetTempPath(), "sample.md");

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += value.Length;
        }

        return count;
    }
}
