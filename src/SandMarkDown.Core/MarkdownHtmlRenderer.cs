using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace SandMarkDown.Core;

/// <summary>
/// Renders Markdown to a dark-theme HTML page the viewer can display offline.
/// </summary>
public static partial class MarkdownHtmlRenderer
{
    /// <summary>
    /// Host name used for the preview page and local files referenced by it.
    /// </summary>
    public const string VirtualHost = "sandmarkdown.local";

    /// <summary>
    /// Absolute URL of the generated preview page.
    /// </summary>
    public const string PreviewUrl = $"https://{VirtualHost}/preview";

    private const string TokenResourceName = "SandMarkDown.Core.tokens.css";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseTaskLists()
        .UsePipeTables()
        .UseGridTables()
        .UseListExtras()
        .UseFootnotes()
        .UseGenericAttributes()
        .UseYamlFrontMatter()
        .Build();

    private static readonly string TokensCss = LoadTokens();

    /// <summary>
    /// Renders <paramref name="markdown"/> as HTML with local links rewritten for the viewer.
    /// </summary>
    /// <param name="markdown">Markdown source. An empty file still produces a page.</param>
    /// <param name="documentPath">Path of the file being read. Relative links resolve against its folder.</param>
    /// <returns>The HTML page and the local files it is allowed to request.</returns>
    public static RenderedDocument Render(string markdown, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var fullDocument = Path.GetFullPath(documentPath);
        var directory = Path.GetDirectoryName(fullDocument);
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        var body = Markdown.ToHtml(markdown, Pipeline);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = "<p class=\"smt-status\">This file is empty.</p>";
        }
        else if (!string.IsNullOrEmpty(directory))
        {
            body = RewriteLocalUrls(body, directory, resources);
        }

        var title = Path.GetFileName(fullDocument);
        return new RenderedDocument(Wrap(body, title), resources, fullDocument);
    }

    /// <summary>
    /// Builds the page shown when no file is open.
    /// </summary>
    /// <returns>A branded welcome page with no local file resources.</returns>
    public static RenderedDocument Welcome()
    {
        var body = $"""
            <h1>{WebUtility.HtmlEncode(Branding.ProductName)}</h1>
            <p>{WebUtility.HtmlEncode(Branding.Description)}</p>
            <p>Choose File, Open, or double-click a Markdown file.</p>
            <p>Ctrl+mouse wheel, Ctrl+Plus, and Ctrl+Minus change the reading size. Ctrl+0 returns to actual size.</p>
            """;
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        return new RenderedDocument(Wrap(body, Branding.ProductName), resources, null);
    }

    private static string RewriteLocalUrls(
        string html,
        string directory,
        Dictionary<string, string> resources)
    {
        return LocalUrlAttribute().Replace(html, match =>
        {
            var urlGroup = match.Groups["url"];
            var rewritten = RewriteUrl(urlGroup.Value, directory, resources);
            var relativeStart = urlGroup.Index - match.Index;
            return string.Concat(
                match.Value[..relativeStart],
                rewritten,
                match.Value[(relativeStart + urlGroup.Length)..]);
        });
    }

    private static string RewriteUrl(
        string url,
        string directory,
        Dictionary<string, string> resources)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#'))
        {
            return url;
        }

        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return "#";
        }

        if (HasRemoteScheme(url))
        {
            return url;
        }

        if (!TrySplitSuffix(url, out var pathPart, out var suffix))
        {
            return url;
        }

        string fullPath;
        if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(pathPart, UriKind.Absolute, out var fileUri) || !fileUri.IsFile)
            {
                return url;
            }

            fullPath = fileUri.LocalPath;
        }
        else
        {
            var decoded = Uri.UnescapeDataString(pathPart.Replace('\\', '/'));
            fullPath = Path.IsPathRooted(decoded)
                ? Path.GetFullPath(decoded)
                : Path.GetFullPath(Path.Combine(directory, decoded));
        }

        var token = EncodeToken(fullPath);
        resources[token] = fullPath;
        return $"https://{VirtualHost}/local/{token}{suffix}";
    }

    private static bool HasRemoteScheme(string url)
    {
        if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var colon = url.IndexOf(':');
        return colon > 1;
    }

    private static bool TrySplitSuffix(string url, out string path, out string suffix)
    {
        var hash = url.IndexOf('#');
        var query = url.IndexOf('?');
        var cut = -1;
        if (hash >= 0 && query >= 0)
        {
            cut = Math.Min(hash, query);
        }
        else if (hash >= 0)
        {
            cut = hash;
        }
        else
        {
            cut = query;
        }

        if (cut == 0)
        {
            path = string.Empty;
            suffix = url;
            return false;
        }

        if (cut > 0)
        {
            path = url[..cut];
            suffix = url[cut..];
        }
        else
        {
            path = url;
            suffix = string.Empty;
        }

        return path.Length > 0;
    }

    private static string EncodeToken(string fullPath)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Wrap(string body, string title)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        return PageTemplate
            .Replace("@@TITLE@@", encodedTitle, StringComparison.Ordinal)
            .Replace("@@TOKENS@@", TokensCss, StringComparison.Ordinal)
            .Replace("@@BODY@@", body, StringComparison.Ordinal);
    }

    private const string PageTemplate = """
        <!DOCTYPE html>
        <html lang="en" data-theme="dark">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>@@TITLE@@</title>
        <style>
        @@TOKENS@@
        html[data-theme="dark"], html[data-theme="dark"] body {
          background: var(--smt-page);
          color: var(--smt-ink);
        }
        .smt-main {
          max-width: 60rem;
          margin-inline: auto;
        }
        .smt-prose {
          max-width: none;
        }
        .smt-prose p,
        .smt-prose ul,
        .smt-prose ol,
        .smt-prose blockquote {
          max-width: 70ch;
        }
        .smt-prose img {
          max-width: 100%;
          height: auto;
        }
        .smt-prose pre {
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: var(--smt-radius);
          padding: var(--smt-space-4);
          overflow: auto;
        }
        .smt-prose :not(pre) > code {
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: 0.25rem;
          padding: 0.05em 0.35em;
        }
        .smt-prose blockquote {
          margin-inline: 0;
          padding-left: 1rem;
          border-left: 3px solid var(--smt-accent);
          color: var(--smt-muted);
        }
        .smt-prose table {
          border-collapse: collapse;
          margin: 1rem 0;
        }
        .smt-prose th,
        .smt-prose td {
          border: 1px solid var(--smt-line);
          padding: 0.4rem 0.65rem;
          text-align: left;
        }
        .smt-prose th {
          background: var(--smt-surface);
        }
        .smt-prose hr {
          border: 0;
          border-top: 1px solid var(--smt-line);
        }
        .smt-prose .task-list-item {
          list-style: none;
        }
        h1 { font-size: 1.85rem; }
        h2 { font-size: 1.4rem; margin-top: 1.6rem; }
        h3 { font-size: 1.15rem; }
        </style>
        </head>
        <body>
        <main class="smt-main">
        <article class="smt-prose">
        @@BODY@@
        </article>
        </main>
        <script>
        (function () {
          function post(message) {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.postMessage(message);
            }
          }
          window.addEventListener("wheel", function (e) {
            if (!e.ctrlKey) return;
            e.preventDefault();
            post({ type: e.deltaY > 0 ? "zoomOut" : "zoomIn" });
          }, { passive: false });
          window.addEventListener("keydown", function (e) {
            if (!e.ctrlKey || e.altKey || e.metaKey) return;
            var key = e.key;
            if (key === "+" || key === "=") {
              e.preventDefault();
              post({ type: "zoomIn" });
            } else if (key === "-" || key === "_") {
              e.preventDefault();
              post({ type: "zoomOut" });
            } else if (key === "0") {
              e.preventDefault();
              post({ type: "zoomReset" });
            } else if (key === "o" || key === "O") {
              e.preventDefault();
              post({ type: "open" });
            } else if (key === "w" || key === "W") {
              e.preventDefault();
              post({ type: "close" });
            }
          });
        })();
        </script>
        </body>
        </html>
        """;

    private static string LoadTokens()
    {
        using var stream = typeof(MarkdownHtmlRenderer).Assembly.GetManifestResourceStream(TokenResourceName)
            ?? throw new InvalidOperationException($"Missing embedded brand tokens '{TokenResourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(
        """<(?<tag>a|img)\b(?<before>[^>]*?)\b(?<attr>href|src)\s*=\s*(?<q>["'])(?<url>[^"']*)\k<q>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalUrlAttribute();
}
