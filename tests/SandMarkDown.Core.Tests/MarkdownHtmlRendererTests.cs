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
        Assert.Contains("<table>", rendered.Html, StringComparison.Ordinal);
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

    private static string SamplePath() => Path.Combine(Path.GetTempPath(), "sample.md");
}
