using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SandMarkDown.Core;

/// <summary>
/// Builds the About page. Its text starts as Markdown in an editor pane, types itself out,
/// then renders through the same pipeline the reader uses.
/// </summary>
public static partial class AboutPage
{
    /// <summary>
    /// Libraries SandMarkDown ships with, and their licenses.
    /// </summary>
    public static IReadOnlyList<(string Name, string License, string Url)> Credits { get; } =
    [
        ("Markdig", "BSD 2-Clause", "https://github.com/xoofx/markdig"),
        ("ColorCode-Universal", "MIT", "https://github.com/CommunityToolkit/ColorCode-Universal"),
        ("Microsoft Edge WebView2 SDK", "BSD-style", "https://aka.ms/webview2"),
        (".NET", "MIT", "https://github.com/dotnet/runtime"),
    ];

    /// <summary>
    /// Markdown shown in the editor pane and rendered below it.
    /// </summary>
    /// <param name="report">Version details for the version line.</param>
    /// <returns>Short Markdown with lines that fit the pane.</returns>
    public static string Markdown(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return string.Join(
            "\n",
            $"# {Branding.ProductName}",
            string.Empty,
            "**A local Markdown reader.** It opens a file,",
            "renders it, and gets out of the way.",
            string.Empty,
            $"- Version {report.DisplayVersion}",
            "- No accounts. No tracking. No telemetry.",
            $"- By [{Branding.CompanyName}]({Branding.WebsiteUrl})");
    }

    /// <summary>
    /// Builds the About page.
    /// </summary>
    /// <param name="report">Version details shown and copied.</param>
    /// <param name="logoDataUri">The Sandmark logo as a <c>data:</c> URI.</param>
    /// <returns>A page with its Content Security Policy.</returns>
    public static RenderedDocument Build(DiagnosticReport report, string logoDataUri)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(logoDataUri);
        if (!logoDataUri.StartsWith("data:image/", StringComparison.Ordinal))
        {
            throw new ArgumentException("The logo must be an image data URI.", nameof(logoDataUri));
        }

        var markdown = Markdown(report);
        var (sourceHtml, revealMs) = SourceLines(markdown);
        var renderedHtml = MarkdownHtmlRenderer.ToHtmlFragment(markdown);
        var policy = ContentSecurityPolicy.Create(showRemoteImages: false);

        var credits = new StringBuilder();
        foreach (var (name, license, url) in Credits)
        {
            credits.Append("<li><a href=\"");
            credits.Append(WebUtility.HtmlEncode(url));
            credits.Append("\">");
            credits.Append(WebUtility.HtmlEncode(name));
            credits.Append("</a><span>");
            credits.Append(WebUtility.HtmlEncode(license));
            credits.Append("</span></li>");
        }

        var html = Template
            .Replace("@@CSP@@", WebUtility.HtmlEncode(policy.Header), StringComparison.Ordinal)
            .Replace("@@NONCE@@", policy.Nonce, StringComparison.Ordinal)
            .Replace("@@TOKENS@@", MarkdownHtmlRenderer.TokensCss, StringComparison.Ordinal)
            .Replace("@@TITLE@@", WebUtility.HtmlEncode($"About {Branding.ProductName}"), StringComparison.Ordinal)
            .Replace("@@LOGO@@", WebUtility.HtmlEncode(logoDataUri), StringComparison.Ordinal)
            .Replace("@@REVEAL@@", revealMs.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("@@SOURCE@@", sourceHtml, StringComparison.Ordinal)
            .Replace("@@RENDERED@@", renderedHtml, StringComparison.Ordinal)
            .Replace("@@WEBSITE@@", WebUtility.HtmlEncode(Branding.WebsiteUrl), StringComparison.Ordinal)
            .Replace("@@PRIVACY@@", WebUtility.HtmlEncode(Branding.PrivacyUrl), StringComparison.Ordinal)
            .Replace("@@SOURCEURL@@", WebUtility.HtmlEncode(Branding.SourceUrl), StringComparison.Ordinal)
            .Replace("@@LICENSE@@", WebUtility.HtmlEncode(Branding.LicenseUrl), StringComparison.Ordinal)
            .Replace("@@LICENSENAME@@", WebUtility.HtmlEncode(Branding.LicenseName), StringComparison.Ordinal)
            .Replace("@@SUPPORT@@", WebUtility.HtmlEncode(Branding.SupportUrl), StringComparison.Ordinal)
            .Replace("@@CREDITS@@", credits.ToString(), StringComparison.Ordinal)
            .Replace("@@COPYRIGHT@@", WebUtility.HtmlEncode(Branding.Copyright), StringComparison.Ordinal);

        return new RenderedDocument(
            html,
            new Dictionary<string, string>(StringComparer.Ordinal),
            null,
            policy.Header,
            hiddenRemoteImages: 0);
    }

    // Each source line types out in turn. Returns the lines as tinted HTML and when rendering starts.
    private static (string Html, int RevealMs) SourceLines(string markdown)
    {
        var builder = new StringBuilder();
        var delay = 250;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.Length == 0)
            {
                builder.Append("<span class=\"src-line src-blank\"> </span>");
                delay += 50;
                continue;
            }

            var duration = Math.Max(90, line.Length * 7);
            builder.Append(CultureInfo.InvariantCulture, $"<span class=\"src-line\" style=\"--n:{line.Length};--d:{delay}ms;--t:{duration}ms\">");
            builder.Append(Tint(line));
            builder.Append("</span>");
            delay += duration + 35;
        }

        return (builder.ToString(), delay + 380);
    }

    private static string Tint(string line)
    {
        var html = WebUtility.HtmlEncode(line);
        if (html.StartsWith("# ", StringComparison.Ordinal))
        {
            return $"<span class=\"t-mark\">#</span> <span class=\"t-head\">{html[2..]}</span>";
        }

        var prefix = string.Empty;
        if (html.StartsWith("- ", StringComparison.Ordinal))
        {
            prefix = "<span class=\"t-mark\">-</span> ";
            html = html[2..];
        }

        html = BoldSource().Replace(html, "<span class=\"t-mark\">**</span><span class=\"t-bold\">$1</span><span class=\"t-mark\">**</span>");
        html = LinkSource().Replace(html, "<span class=\"t-mark\">[</span><span class=\"t-link\">$1</span><span class=\"t-mark\">](</span><span class=\"t-url\">$2</span><span class=\"t-mark\">)</span>");
        return prefix + html;
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex BoldSource();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkSource();

    private const string Template = """
        <!DOCTYPE html>
        <html lang="en" data-theme="dark">
        <head>
        <meta charset="utf-8">
        <meta http-equiv="Content-Security-Policy" content="@@CSP@@">
        <meta name="referrer" content="no-referrer">
        <title>@@TITLE@@</title>
        <style>
        @@TOKENS@@
        html, body {
          margin: 0;
          height: 100%;
          background: var(--smt-page);
          color: var(--smt-ink);
          overflow: hidden;
          user-select: none;
        }
        body {
          display: flex;
          flex-direction: column;
          gap: 1rem;
          padding: 1.25rem 1.5rem 1.1rem;
          box-sizing: border-box;
          background:
            radial-gradient(42rem 18rem at 50% -6rem, color-mix(in srgb, var(--smt-accent) 14%, transparent), transparent 70%),
            var(--smt-page);
        }
        .plate {
          display: flex;
          justify-content: center;
          align-items: center;
          background: #ffffff;
          border-radius: 0.75rem;
          padding: 0.85rem 1rem;
          box-shadow: 0 0.5rem 1.5rem rgb(0 0 0 / 0.35);
        }
        .plate img { height: 4.25rem; width: auto; display: block; }
        .stage {
          display: grid;
          min-height: 13.5rem;
          position: relative;
        }
        .stage > * { grid-area: 1 / 1; }
        .source {
          margin: 0;
          padding: 0.9rem 1rem;
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: var(--smt-radius);
          font: 0.8rem/1.6 var(--smt-mono);
          color: var(--smt-ink);
          cursor: pointer;
          visibility: hidden;
          overflow: hidden;
        }
        .source-bar {
          display: flex;
          gap: 0.35rem;
          margin-bottom: 0.6rem;
          color: var(--smt-muted);
          font: 0.7rem var(--smt-font);
          letter-spacing: 0.04em;
          text-transform: uppercase;
          align-items: center;
        }
        .source-bar i {
          width: 0.55rem; height: 0.55rem; border-radius: 50%;
          background: var(--smt-line); display: inline-block;
        }
        .source-bar span { margin-left: 0.4rem; }
        .src-line {
          display: block;
          white-space: pre;
          overflow: hidden;
          width: calc(var(--n) * 1ch + 2px);
          border-right: 2px solid transparent;
          box-sizing: content-box;
        }
        .src-blank { width: auto; }
        .t-mark { color: var(--smt-muted); }
        .t-head { color: var(--smt-accent); font-weight: 700; }
        .t-bold { color: var(--smt-ink); font-weight: 700; }
        .t-link { color: var(--smt-accent); }
        .t-url { color: var(--smt-muted); text-decoration: underline; text-decoration-color: var(--smt-line); }
        .rendered {
          padding: 0.2rem 0.25rem;
          line-height: 1.55;
        }
        .rendered h1 {
          margin: 0 0 0.35rem;
          font-size: 1.9rem;
          letter-spacing: -0.01em;
        }
        .rendered p { margin: 0 0 0.6rem; }
        .rendered ul { margin: 0; padding-left: 1.15rem; color: var(--smt-muted); }
        .rendered li { margin: 0.15rem 0; }
        .rendered li::marker { color: var(--smt-accent); }
        .scan {
          pointer-events: none;
          align-self: start;
          height: 2px;
          opacity: 0;
          background: linear-gradient(90deg, transparent, var(--smt-accent), transparent);
          box-shadow: 0 0 12px var(--smt-accent);
        }
        .links {
          display: flex;
          flex-wrap: wrap;
          gap: 0.35rem 1.1rem;
          font-size: 0.875rem;
        }
        .links a { color: var(--smt-accent-ink); }
        details {
          font-size: 0.85rem;
          color: var(--smt-muted);
        }
        summary { cursor: pointer; }
        summary:focus-visible { outline: 2px solid var(--smt-focus); outline-offset: 2px; }
        details ul { list-style: none; margin: 0.5rem 0 0; padding: 0; }
        details li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.15rem 0; border-bottom: 1px solid var(--smt-line); }
        details li a { color: var(--smt-ink); text-decoration: none; }
        details li a:hover { color: var(--smt-accent); }
        footer {
          margin-top: auto;
          display: flex;
          align-items: center;
          gap: 0.6rem;
          font-size: 0.8rem;
          color: var(--smt-muted);
        }
        footer .copyright { margin-right: auto; }
        button {
          font: 600 0.85rem var(--smt-font);
          border-radius: 0.3rem;
          padding: 0.4rem 0.85rem;
          cursor: pointer;
          border: 1px solid var(--smt-line);
          background: transparent;
          color: var(--smt-ink);
        }
        button:hover { border-color: var(--smt-accent); color: var(--smt-accent); }
        button.primary { background: var(--smt-accent); color: var(--smt-on-accent); border-color: var(--smt-accent); }
        button.primary:hover { background: #a9d0e8; color: var(--smt-on-accent); }
        button:focus-visible, a:focus-visible { outline: 2px solid var(--smt-focus); outline-offset: 2px; }
        .icon-button { padding: 0.4rem 0.6rem; }

        /* Playing: type the source, sweep, then hand over to the rendered page. */
        .play .source { visibility: visible; animation: source-out 280ms ease-in var(--reveal) forwards; }
        .play .src-line:not(.src-blank) {
          width: 0;
          animation:
            type var(--t) steps(var(--n), end) var(--d) forwards,
            caret var(--t) linear var(--d);
        }
        .play .rendered { animation: render-in 520ms cubic-bezier(.2,.7,.2,1) var(--reveal) both; }
        .play .scan { animation: scan 360ms ease-in calc(var(--reveal) - 220ms) both; }
        .done .source { visibility: hidden; animation: none; }
        .done .rendered, .done .scan { animation: none; }
        .done .scan { opacity: 0; }

        @keyframes type { from { width: 0; } to { width: calc(var(--n) * 1ch + 2px); } }
        @keyframes caret { 0%, 99% { border-right-color: var(--smt-accent); } 100% { border-right-color: transparent; } }
        @keyframes source-out { to { opacity: 0; transform: scale(0.985); visibility: hidden; } }
        @keyframes render-in { from { opacity: 0; transform: translateY(8px); filter: blur(2px); } to { opacity: 1; transform: none; filter: none; } }
        @keyframes scan {
          0% { opacity: 0; transform: translateY(0); }
          15% { opacity: 1; }
          100% { opacity: 0; transform: translateY(12.5rem); }
        }
        @media (prefers-reduced-motion: reduce) {
          .play .source { visibility: hidden; animation: none; }
          .play .rendered, .play .scan, .play .src-line:not(.src-blank) { animation: none; }
        }
        </style>
        </head>
        <body style="--reveal: @@REVEAL@@ms">
        <div class="plate"><img src="@@LOGO@@" alt="Sandmark Technologies"></div>
        <main class="stage" aria-live="polite">
          <pre class="source" id="about-source" aria-hidden="true" title="Click to skip"><span class="source-bar"><i></i><i></i><i></i><span>about.md</span></span>@@SOURCE@@</pre>
          <article class="rendered smt-prose" id="about-rendered">@@RENDERED@@</article>
          <div class="scan" aria-hidden="true"></div>
        </main>
        <nav class="links" aria-label="Links">
          <a href="@@WEBSITE@@">sandmarktech.com</a>
          <a href="@@PRIVACY@@">Privacy</a>
          <a href="@@LICENSE@@">@@LICENSENAME@@</a>
          <a href="@@SOURCEURL@@">Source code</a>
          <a href="@@SUPPORT@@">Report a problem</a>
        </nav>
        <details>
          <summary>Open-source credits</summary>
          <ul>@@CREDITS@@</ul>
        </details>
        <footer>
          <span class="copyright">@@COPYRIGHT@@</span>
          <button type="button" class="icon-button" id="about-replay" title="Play again" aria-label="Play the About animation again">&#x21bb;</button>
          <button type="button" id="about-copy">Copy version info</button>
          <button type="button" class="primary" id="about-close">Close</button>
        </footer>
        <script nonce="@@NONCE@@">
        (function () {
          var root = document.documentElement;
          function post(message) {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.postMessage(message);
            }
          }
          function play() {
            root.classList.remove("play", "done");
            void root.offsetWidth;
            root.classList.add("play");
          }
          function finish() {
            root.classList.remove("play");
            root.classList.add("done");
          }
          document.getElementById("about-source").addEventListener("click", finish);
          document.getElementById("about-replay").addEventListener("click", play);
          var copy = document.getElementById("about-copy");
          copy.addEventListener("click", function () {
            post({ type: "copy" });
            copy.textContent = "Copied";
            window.setTimeout(function () { copy.textContent = "Copy version info"; }, 1600);
          });
          document.getElementById("about-close").addEventListener("click", function () { post({ type: "close" }); });
          var code = "";
          window.addEventListener("keydown", function (e) {
            if (!e.ctrlKey && !e.altKey && !e.metaKey && /^[a-z]$/i.test(e.key)) {
              code = (code + e.key.toLowerCase()).slice(-3);
              if (code === "smt") {
                code = "";
                post({ type: "canyon" });
              }
            }
            if (e.key === "Escape") {
              post({ type: "close" });
            } else if ((e.key === " " || e.key === "Enter") && root.classList.contains("play") && e.target === document.body) {
              finish();
            }
          });
          play();
          document.getElementById("about-close").focus();
        })();
        </script>
        </body>
        </html>
        """;
}
