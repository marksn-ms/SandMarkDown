using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the error log, window placement, version details, and About page.
/// </summary>
public class PolishTests
{
    /// <summary>
    /// An error entry names the product, version, and exception, and lands in error.log.
    /// </summary>
    [Fact]
    public void Error_log_appends_an_entry()
    {
        var directory = TempDirectory();
        var when = new DateTimeOffset(2026, 11, 4, 16, 30, 0, TimeSpan.FromHours(-5));

        var path = ErrorLog.Write(directory, new InvalidOperationException("boom"), when, "1.0.0 (abc1234)");
        ErrorLog.Write(directory, new InvalidOperationException("again"), when, "1.0.0 (abc1234)");

        var text = File.ReadAllText(path);
        Assert.EndsWith("error.log", path, StringComparison.Ordinal);
        Assert.Contains("2026-11-04 16:30:00 -05:00  SandMarkDown 1.0.0 (abc1234)", text, StringComparison.Ordinal);
        Assert.Contains("boom", text, StringComparison.Ordinal);
        Assert.Contains("again", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A log past its size limit is kept as error.old.log and a new file starts.
    /// </summary>
    [Fact]
    public void Error_log_rolls_over_when_large()
    {
        var directory = TempDirectory();
        File.WriteAllText(Path.Combine(directory, "error.log"), new string('x', (int)ErrorLog.MaxBytes + 10));

        var path = ErrorLog.Write(directory, new InvalidOperationException("fresh"), DateTimeOffset.Now, "1.0.0");

        Assert.True(File.Exists(Path.Combine(directory, "error.old.log")));
        Assert.True(new FileInfo(path).Length < ErrorLog.MaxBytes);
    }

    /// <summary>
    /// Placement round-trips through its file.
    /// </summary>
    [Fact]
    public void Window_placement_round_trips()
    {
        var path = Path.Combine(TempDirectory(), "window.json");
        new WindowPlacement(100, 80, 1200, 900, IsMaximized: true).Save(path);

        Assert.Equal(new WindowPlacement(100, 80, 1200, 900, true), WindowPlacement.Load(path));
    }

    /// <summary>
    /// Missing or broken placement files are ignored.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{ "left": 0, "top": 0, "width": 0, "height": 500 }""")]
    public void Window_placement_ignores_unusable_files(string? contents)
    {
        var path = Path.Combine(TempDirectory(), "window.json");
        if (contents is not null)
        {
            File.WriteAllText(path, contents);
        }

        Assert.Null(WindowPlacement.Load(path));
    }

    /// <summary>
    /// A window left on an unplugged monitor comes back onto the desktop.
    /// </summary>
    [Fact]
    public void Window_placement_fits_back_on_screen()
    {
        var offScreen = new WindowPlacement(4000, 2500, 1100, 860, false);

        var fitted = offScreen.FitTo(0, 0, 1920, 1080, 720, 480);

        Assert.Equal(1920 - 1100, fitted.Left);
        Assert.Equal(1080 - 860, fitted.Top);
        Assert.Equal(1100, fitted.Width);
    }

    /// <summary>
    /// A window larger than the desktop shrinks to fit.
    /// </summary>
    [Fact]
    public void Window_placement_shrinks_to_the_desktop()
    {
        var fitted = new WindowPlacement(-50, -50, 5000, 4000, false).FitTo(0, 0, 1366, 768, 720, 480);
        Assert.Equal(new WindowPlacement(0, 0, 1366, 768, false), fitted);
    }

    /// <summary>
    /// The informational version splits into version and short commit.
    /// </summary>
    [Theory]
    [InlineData("1.0.0+c822b43d1e2f3a4b", "1.0.0", "c822b43")]
    [InlineData("1.0.0", "1.0.0", null)]
    [InlineData(null, "0.9.0", null)]
    public void Informational_version_parses(string? informational, string version, string? commit)
    {
        var parsed = DiagnosticReport.ParseInformationalVersion(informational, "0.9.0");
        Assert.Equal(version, parsed.Version);
        Assert.Equal(commit, parsed.Commit);
    }

    /// <summary>
    /// Copied version info has everything a support request needs.
    /// </summary>
    [Fact]
    public void Diagnostic_report_text_lists_versions()
    {
        var text = new DiagnosticReport("1.0.0", "c822b43", "Windows 11", ".NET 10.0.4", null).ToString();
        Assert.Contains("SandMarkDown 1.0.0 (c822b43)", text, StringComparison.Ordinal);
        Assert.Contains("Windows: Windows 11", text, StringComparison.Ordinal);
        Assert.Contains("WebView2: not available", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The About page carries the source, the rendered text, required links, and a locked-down policy.
    /// </summary>
    [Fact]
    public void About_page_shows_source_rendered_text_and_links()
    {
        var report = new DiagnosticReport("1.0.0", "c822b43", "Windows 11", ".NET 10", "142.0");
        var about = AboutPage.Build(report, "data:image/jpeg;base64,AAAA");

        Assert.Contains("class=\"source\"", about.Html, StringComparison.Ordinal);
        Assert.Contains("<h1", about.Html, StringComparison.Ordinal);
        Assert.Contains("Version 1.0.0 (c822b43)", about.Html, StringComparison.Ordinal);
        Assert.Contains(Branding.WebsiteUrl, about.Html, StringComparison.Ordinal);
        Assert.Contains("privacy.html", about.Html, StringComparison.Ordinal);
        Assert.Contains("LICENSE", about.Html, StringComparison.Ordinal);
        Assert.Contains("Markdig", about.Html, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", about.Html, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", about.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("@@", about.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every About source line fits the editor pane without clipping.
    /// </summary>
    [Fact]
    public void About_source_lines_fit_the_pane()
    {
        var markdown = AboutPage.Markdown(new DiagnosticReport("10.20.30", "abcdef1", "w", "n", null));
        Assert.All(markdown.Split('\n'), line => Assert.True(line.Length <= 62, line));
    }

    /// <summary>
    /// The logo must be an image data URI so the page needs no file or network access.
    /// </summary>
    [Fact]
    public void About_page_rejects_a_non_image_logo()
    {
        var report = new DiagnosticReport("1.0.0", null, "w", "n", null);
        Assert.Throws<ArgumentException>(() => AboutPage.Build(report, "https://example.com/logo.png"));
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sandmarkdown-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
