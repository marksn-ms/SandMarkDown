using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ColorCode;
using ColorCode.Styling;
using Markdig;

namespace SandMarkDown.Core;

/// <summary>
/// Renders Markdown to a dark-theme HTML page the viewer can display offline.
/// The page can find text, list headings when the file has several, and color fenced code.
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

    private static readonly HtmlFormatter CodeFormatter = new(StyleDictionary.DefaultDark);

    /// <summary>
    /// Renders <paramref name="markdown"/> as HTML with local links rewritten for the viewer.
    /// The page runs only the viewer's own script. Markup in the file cannot run script or reach the network.
    /// </summary>
    /// <param name="markdown">Markdown source. An empty file still produces a page.</param>
    /// <param name="documentPath">Path of the file being read. Relative links resolve against its folder.</param>
    /// <param name="showRemoteImages">
    /// When <see langword="false"/>, images hosted on the web are hidden until the reader asks for them.
    /// </param>
    /// <returns>The HTML page and the local files it is allowed to request.</returns>
    public static RenderedDocument Render(string markdown, string documentPath, bool showRemoteImages = false)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var fullDocument = Path.GetFullPath(documentPath);
        var directory = Path.GetDirectoryName(fullDocument);
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        var hiddenRemoteImages = 0;
        var body = Markdown.ToHtml(markdown, Pipeline);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = "<p class=\"smt-status\">This file is empty.</p>";
        }
        else
        {
            body = HighlightFencedCode(body);
            if (!string.IsNullOrEmpty(directory))
            {
                var context = new UrlContext(fullDocument, directory, resources, showRemoteImages);
                body = RewriteUrls(body, context);
                hiddenRemoteImages = context.HiddenRemoteImages;
            }

            body = WrapTables(body);
        }

        var notice = hiddenRemoteImages > 0 ? RemoteImageNotice(hiddenRemoteImages) : string.Empty;
        var title = Path.GetFileName(fullDocument);
        var policy = ContentSecurityPolicy.Create(showRemoteImages);
        return new RenderedDocument(
            Wrap(notice + body, title, BuildOutline(body), policy),
            resources,
            fullDocument,
            policy.Header,
            hiddenRemoteImages);
    }

    /// <summary>
    /// Builds the page shown when no file is open.
    /// </summary>
    /// <returns>A branded welcome page with no local file resources.</returns>
    public static RenderedDocument Welcome()
    {
        var body = $"""
            <h1>{WebUtility.HtmlEncode(Branding.ProductName)}</h1>
            <p class="smt-lead">{WebUtility.HtmlEncode(Branding.Description)}</p>
            <p>Open a file, or drop one here.</p>
            """;
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        var policy = ContentSecurityPolicy.Create(showRemoteImages: false);
        return new RenderedDocument(
            Wrap(body, Branding.ProductName, string.Empty, policy),
            resources,
            null,
            policy.Header,
            hiddenRemoteImages: 0);
    }

    private sealed class UrlContext(
        string documentPath,
        string directory,
        Dictionary<string, string> resources,
        bool showRemoteImages)
    {
        public string DocumentPath { get; } = documentPath;

        public string Directory { get; } = directory;

        public Dictionary<string, string> Resources { get; } = resources;

        public bool ShowRemoteImages { get; } = showRemoteImages;

        public int HiddenRemoteImages { get; set; }
    }

    private static string RewriteUrls(string html, UrlContext context)
    {
        return LocalUrlAttribute().Replace(html, match =>
        {
            var isImage = match.Groups["tag"].Value.Equals("img", StringComparison.OrdinalIgnoreCase);
            var urlGroup = match.Groups["url"];
            var url = urlGroup.Value.Trim();
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                url = "https:" + url;
            }

            var relativeStart = urlGroup.Index - match.Index;
            var before = match.Value[..relativeStart];
            var after = match.Value[(relativeStart + urlGroup.Length)..];

            if (isImage && IsWebUrl(url) && !context.ShowRemoteImages)
            {
                context.HiddenRemoteImages++;
                var attrGroup = match.Groups["attr"];
                var attrStart = attrGroup.Index - match.Index;
                return string.Concat(
                    match.Value[..attrStart],
                    "data-smt-remote-src",
                    match.Value[(attrStart + attrGroup.Length)..relativeStart],
                    WebUtility.HtmlEncode(WebUtility.HtmlDecode(url)),
                    after);
            }

            return string.Concat(before, RewriteUrl(url, isImage, context), after);
        });
    }

    private static string RewriteUrl(string url, bool isImage, UrlContext context)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#'))
        {
            return url;
        }

        if (HasRemoteScheme(url))
        {
            return IsAllowedScheme(url) ? url : "#";
        }

        if (!TrySplitSuffix(url, out var pathPart, out var suffix))
        {
            return url;
        }

        string fullPath;
        try
        {
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(pathPart, UriKind.Absolute, out var fileUri) || !fileUri.IsFile)
                {
                    return "#";
                }

                fullPath = fileUri.LocalPath;
            }
            else
            {
                var decoded = Uri.UnescapeDataString(WebUtility.HtmlDecode(pathPart)).Replace('/', '\\');
                fullPath = Path.IsPathRooted(decoded)
                    ? Path.GetFullPath(decoded)
                    : Path.GetFullPath(Path.Combine(context.Directory, decoded));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
        {
            return "#";
        }

        // Loading an image from another computer's share sends Windows sign-in details to that computer.
        if (isImage && !MarkdownPaths.IsSameMachineOrShare(context.DocumentPath, fullPath))
        {
            return BlankImage;
        }

        var token = EncodeToken(fullPath);
        context.Resources[token] = fullPath;
        return $"https://{VirtualHost}/local/{token}{suffix}";
    }

    private const string BlankImage = "data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==";

    private static bool IsWebUrl(string url) =>
        url.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedScheme(string url) =>
        IsWebUrl(url) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static string RemoteImageNotice(int count)
    {
        var noun = count == 1 ? "image from the web is" : "images from the web are";
        return $"""
            <div class="smt-remote" role="status">
            <span>{count} {noun} hidden. Showing them tells those sites you opened this file.</span>
            <button type="button" id="smt-show-remote">Show images</button>
            </div>
            """;
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

    private static string Wrap(string body, string title, string outline, ContentSecurityPolicy policy)
    {
        var page = PageTemplate
            .Replace("@@CSP@@", WebUtility.HtmlEncode(policy.Header), StringComparison.Ordinal)
            .Replace("@@NONCE@@", policy.Nonce, StringComparison.Ordinal)
            .Replace("@@TITLE@@", WebUtility.HtmlEncode(title), StringComparison.Ordinal)
            .Replace("@@TOKENS@@", TokensCss, StringComparison.Ordinal);

        // The body and outline come from the file. Splicing them in, instead of calling Replace,
        // keeps placeholder text written inside the file from being expanded.
        var bodyAt = page.IndexOf("@@BODY@@", StringComparison.Ordinal);
        var outlineAt = page.IndexOf("@@OUTLINE@@", StringComparison.Ordinal);
        return string.Concat(
            page[..bodyAt],
            body,
            page[(bodyAt + "@@BODY@@".Length)..outlineAt],
            outline,
            page[(outlineAt + "@@OUTLINE@@".Length)..]);
    }

    private static string BuildOutline(string html)
    {
        var items = new List<(int Level, string Id, string Label)>();
        foreach (Match match in Heading().Matches(html))
        {
            if (!int.TryParse(match.Groups["level"].Value, out var level))
            {
                continue;
            }

            var label = WebUtility.HtmlDecode(HtmlTag().Replace(match.Groups["inner"].Value, string.Empty));
            label = string.Join(' ', label.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            if (label.Length == 0)
            {
                continue;
            }

            items.Add((level, match.Groups["id"].Value, label));
        }

        if (items.Count < 2)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("<nav class=\"smt-outline\" aria-label=\"On this page\"><p>On this page</p><ol>");
        foreach (var item in items)
        {
            builder.Append("<li class=\"smt-h");
            builder.Append(item.Level);
            builder.Append("\"><a href=\"#");
            builder.Append(WebUtility.HtmlEncode(item.Id));
            builder.Append("\">");
            builder.Append(WebUtility.HtmlEncode(item.Label));
            builder.Append("</a></li>");
        }

        builder.Append("</ol></nav>");
        return builder.ToString();
    }

    private static string WrapTables(string html)
    {
        return TableElement().Replace(
            html,
            match => $"<div class=\"smt-table-scroll\">{match.Value}</div>");
    }

    private static string HighlightFencedCode(string html)
    {
        return FencedCode().Replace(html, match =>
        {
            var lang = match.Groups["lang"].Value;
            var language = Languages.FindById(lang) ?? Languages.FindById(lang.ToLowerInvariant());
            if (language is null)
            {
                return match.Value;
            }

            var source = WebUtility.HtmlDecode(match.Groups["code"].Value);
            var colored = CodeFormatter.GetHtmlString(source, language);
            var inner = PreInner().Match(colored);
            if (!inner.Success)
            {
                return match.Value;
            }

            return $"<pre><code class=\"language-{WebUtility.HtmlEncode(lang)}\">{inner.Groups["inner"].Value}</code></pre>";
        });
    }

    private const string PageTemplate = """
        <!DOCTYPE html>
        <html lang="en" data-theme="dark">
        <head>
        <meta charset="utf-8">
        <meta http-equiv="Content-Security-Policy" content="@@CSP@@">
        <meta http-equiv="x-dns-prefetch-control" content="off">
        <meta name="referrer" content="no-referrer">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>@@TITLE@@</title>
        <style>
        @@TOKENS@@
        html[data-theme="dark"], html[data-theme="dark"] body {
          background: var(--smt-page);
          color: var(--smt-ink);
        }
        ::selection {
          background: var(--smt-accent);
          color: var(--smt-on-accent);
        }
        .smt-main {
          display: grid;
          grid-template-columns: minmax(0, 1fr);
          gap: var(--smt-space-6);
          width: min(100% - (2 * var(--smt-page-pad)), 60rem);
          margin-inline: auto;
          align-items: start;
        }
        .smt-prose {
          max-width: none;
          min-width: 0;
          line-height: 1.65;
        }
        .smt-prose p,
        .smt-prose ul,
        .smt-prose ol,
        .smt-prose blockquote {
          max-width: 70ch;
        }
        .smt-prose p {
          margin: 0.85rem 0;
        }
        .smt-lead {
          font-size: 1.125rem;
          color: var(--smt-muted);
        }
        .smt-prose img {
          max-width: 100%;
          height: auto;
          border-radius: var(--smt-radius);
        }
        .smt-prose pre {
          max-width: 100%;
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: var(--smt-radius);
          padding: var(--smt-space-4);
          overflow: auto;
          font-size: 0.9rem;
          line-height: 1.5;
          tab-size: 4;
        }
        .smt-prose :not(pre) > code {
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: 0.25rem;
          padding: 0.05em 0.35em;
          font-size: 0.92em;
        }
        .smt-prose blockquote {
          margin: 1rem 0;
          margin-inline: 0;
          padding-left: 1rem;
          border-left: 3px solid var(--smt-accent);
          color: var(--smt-muted);
        }
        .smt-prose ul,
        .smt-prose ol {
          padding-left: 1.4rem;
        }
        .smt-prose li {
          margin: 0.25rem 0;
        }
        .smt-table-scroll {
          max-width: 100%;
          overflow-x: auto;
          margin: 1rem 0;
        }
        .smt-prose table {
          width: max-content;
          max-width: 100%;
          border-collapse: collapse;
          margin: 0;
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
          margin: 2rem 0;
        }
        .smt-prose .task-list-item {
          list-style: none;
          margin-left: -1.25rem;
        }
        .smt-prose .task-list-item input {
          accent-color: var(--smt-accent);
          margin-right: 0.4rem;
        }
        .smt-prose .footnotes {
          margin-top: 2.5rem;
          color: var(--smt-muted);
          font-size: 0.92rem;
        }
        .smt-prose h1,
        .smt-prose h2,
        .smt-prose h3,
        .smt-prose h4 {
          line-height: 1.25;
          scroll-margin-top: 1.25rem;
        }
        .smt-prose h1 { font-size: 2rem; margin-top: 0; }
        .smt-prose h2 {
          font-size: 1.5rem;
          margin-top: 2rem;
          padding-top: 1.15rem;
          border-top: 1px solid var(--smt-line);
        }
        .smt-prose h1 + h2 {
          border-top: 0;
          padding-top: 0;
        }
        .smt-prose h2:first-child {
          border-top: 0;
          padding-top: 0;
          margin-top: 0;
        }
        .smt-prose h3 { font-size: 1.2rem; }
        .smt-outline {
          font-size: 0.875rem;
          line-height: 1.4;
        }
        .smt-outline p {
          margin: 0 0 0.5rem;
          color: var(--smt-muted);
          font-size: 0.75rem;
          letter-spacing: 0.04em;
          text-transform: uppercase;
        }
        .smt-outline ol {
          list-style: none;
          margin: 0;
          padding: 0;
        }
        .smt-outline a {
          display: block;
          color: var(--smt-muted);
          text-decoration: none;
          padding: 0.15rem 0;
        }
        .smt-outline a:hover {
          color: var(--smt-accent);
        }
        .smt-outline .smt-h2 { padding-left: 0.75rem; }
        .smt-outline .smt-h3 { padding-left: 1.5rem; }
        .smt-find {
          position: fixed;
          top: 0.75rem;
          right: 0.75rem;
          z-index: 2;
          display: flex;
          flex-wrap: wrap;
          gap: 0.35rem;
          align-items: center;
          max-width: calc(100% - 1.5rem);
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-radius: var(--smt-radius);
          padding: 0.35rem;
        }
        .smt-find[hidden] { display: none; }
        .smt-remote {
          display: flex;
          flex-wrap: wrap;
          gap: 0.5rem 1rem;
          align-items: center;
          margin: 0 0 1.25rem;
          padding: 0.6rem 0.85rem;
          background: var(--smt-surface);
          border: 1px solid var(--smt-line);
          border-left: 3px solid var(--smt-accent);
          border-radius: var(--smt-radius);
          color: var(--smt-muted);
          font-size: 0.9rem;
        }
        .smt-remote button {
          background: var(--smt-accent);
          color: var(--smt-on-accent);
          border: 0;
          border-radius: 0.25rem;
          padding: 0.3rem 0.75rem;
          font: 600 0.875rem var(--smt-font);
          cursor: pointer;
        }
        .smt-prose img[data-smt-remote-src] {
          display: inline-block;
          min-width: 6rem;
          min-height: 2rem;
          padding: 0.35rem 0.6rem;
          border: 1px dashed var(--smt-line);
          border-radius: var(--smt-radius);
          color: var(--smt-muted);
          font-size: 0.85rem;
        }
        body:has(.smt-find:not([hidden])) .smt-main {
          padding-top: 4.25rem;
        }
        .smt-find input {
          width: 12rem;
          max-width: 100%;
          background: var(--smt-page);
          color: var(--smt-ink);
          border: 1px solid var(--smt-line);
          border-radius: 0.25rem;
          padding: 0.25rem 0.45rem;
          font: inherit;
        }
        .smt-find button {
          background: transparent;
          color: var(--smt-ink);
          border: 0;
          border-radius: 0.25rem;
          font: inherit;
          cursor: pointer;
          padding: 0.2rem 0.45rem;
        }
        .smt-find button:hover { color: var(--smt-accent); }
        #smt-find-count {
          color: var(--smt-muted);
          font-size: 0.8125rem;
          min-width: 5.5rem;
        }
        ::highlight(smt-find) {
          background-color: color-mix(in srgb, var(--smt-accent) 28%, var(--smt-surface));
          color: var(--smt-ink);
        }
        ::highlight(smt-find-current) {
          background-color: var(--smt-accent);
          color: var(--smt-on-accent);
        }
        @media (min-width: 960px) {
          .smt-main:has(.smt-outline) {
            width: min(100% - (2 * var(--smt-page-pad)), 76rem);
            grid-template-columns: minmax(0, 1fr) 15rem;
          }
          .smt-outline {
            position: sticky;
            top: var(--smt-space-5);
          }
        }
        </style>
        </head>
        <body>
        <div id="smt-find" class="smt-find" hidden role="search">
          <input id="smt-find-input" type="search" aria-label="Find" placeholder="Find" autocomplete="off" spellcheck="false">
          <span id="smt-find-count" aria-live="polite"></span>
          <button type="button" id="smt-find-prev">Previous</button>
          <button type="button" id="smt-find-next">Next</button>
          <button type="button" id="smt-find-close" aria-label="Close find">Close</button>
        </div>
        <main class="smt-main">
        <article class="smt-prose">
        @@BODY@@
        </article>
        @@OUTLINE@@
        </main>
        <script nonce="@@NONCE@@">
        (function () {
          var find = document.getElementById("smt-find");
          var findInput = document.getElementById("smt-find-input");
          var findCount = document.getElementById("smt-find-count");
          var matches = [];
          var matchIndex = -1;
          var findTimer = 0;
          var findHighlight = null;
          var currentHighlight = null;
          if (window.Highlight && CSS.highlights) {
            findHighlight = new Highlight();
            currentHighlight = new Highlight();
            CSS.highlights.set("smt-find", findHighlight);
            CSS.highlights.set("smt-find-current", currentHighlight);
          }

          function post(message) {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.postMessage(message);
            }
          }

          function clearMatches() {
            matches = [];
            matchIndex = -1;
            if (findHighlight) findHighlight.clear();
            if (currentHighlight) currentHighlight.clear();
          }

          function updateCount() {
            if (!findInput.value) {
              findCount.textContent = "";
              return;
            }
            findCount.textContent = matches.length === 0
              ? "No matches"
              : (matchIndex + 1) + " of " + matches.length;
          }

          function showMatch(index) {
            if (!matches.length) {
              updateCount();
              return;
            }
            matchIndex = (index + matches.length) % matches.length;
            if (currentHighlight) {
              currentHighlight.clear();
              currentHighlight.add(matches[matchIndex]);
            }
            var rect = matches[matchIndex].getBoundingClientRect();
            window.scrollTo({ top: Math.max(0, rect.top + window.scrollY - 80), behavior: "auto" });
            updateCount();
          }

          function search(query) {
            clearMatches();
            var prose = document.querySelector(".smt-prose");
            if (!query || !prose || !findHighlight) {
              updateCount();
              return;
            }
            var walker = document.createTreeWalker(prose, NodeFilter.SHOW_TEXT);
            var q = query.toLowerCase();
            var node;
            while ((node = walker.nextNode()) && matches.length < 2000) {
              var text = node.textContent || "";
              var lower = text.toLowerCase();
              var from = 0;
              while (from < text.length && matches.length < 2000) {
                var at = lower.indexOf(q, from);
                if (at < 0) break;
                var range = document.createRange();
                range.setStart(node, at);
                range.setEnd(node, at + query.length);
                matches.push(range);
                findHighlight.add(range);
                from = at + Math.max(query.length, 1);
              }
            }
            if (matches.length) showMatch(0);
            else updateCount();
          }

          function openFind() {
            find.hidden = false;
            findInput.focus();
            findInput.select();
          }

          function closeFind() {
            find.hidden = true;
            findInput.value = "";
            clearMatches();
            updateCount();
          }

          function step(delta) {
            if (find.hidden) openFind();
            if (!matches.length) return;
            showMatch(matchIndex + delta);
          }

          findInput.addEventListener("input", function () {
            window.clearTimeout(findTimer);
            findTimer = window.setTimeout(function () { search(findInput.value); }, 60);
          });
          document.getElementById("smt-find-prev").addEventListener("click", function () { step(-1); });
          document.getElementById("smt-find-next").addEventListener("click", function () { step(1); });
          document.getElementById("smt-find-close").addEventListener("click", closeFind);
          var showRemote = document.getElementById("smt-show-remote");
          if (showRemote) {
            showRemote.addEventListener("click", function () { post({ type: "showRemoteImages" }); });
          }
          window.smtFind = {
            open: openFind,
            next: function () { step(1); },
            previous: function () { step(-1); }
          };

          window.addEventListener("wheel", function (e) {
            if (!e.ctrlKey) return;
            e.preventDefault();
            post({ type: e.deltaY > 0 ? "zoomOut" : "zoomIn" });
          }, { passive: false });
          window.addEventListener("keydown", function (e) {
            var typing = e.target && e.target.id === "smt-find-input";
            if (e.key === "Escape" && !find.hidden) {
              e.preventDefault();
              closeFind();
              return;
            }
            if (typing && e.key === "Enter") {
              e.preventDefault();
              step(e.shiftKey ? -1 : 1);
              return;
            }
            if (e.key === "F3") {
              e.preventDefault();
              step(e.shiftKey ? -1 : 1);
              return;
            }
            if (e.altKey && !e.ctrlKey && !e.metaKey && (e.key === "ArrowLeft" || e.key === "ArrowRight")) {
              e.preventDefault();
              post({ type: e.key === "ArrowLeft" ? "back" : "forward" });
              return;
            }
            if (!e.ctrlKey || e.altKey || e.metaKey) return;
            var key = e.key;
            if (key === "f" || key === "F") {
              e.preventDefault();
              openFind();
              return;
            }
            if (typing) return;
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
          window.addEventListener("mouseup", function (e) {
            if (e.button === 3) {
              e.preventDefault();
              post({ type: "back" });
            } else if (e.button === 4) {
              e.preventDefault();
              post({ type: "forward" });
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

    [GeneratedRegex(
        """<h(?<level>[1-3])\b[^>]*\bid="(?<id>[^"]+)"[^>]*>(?<inner>.*?)</h\k<level>>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Heading();

    [GeneratedRegex(
        """<pre><code class="language-(?<lang>[\w#+.-]+)">(?<code>.*?)</code></pre>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex FencedCode();

    [GeneratedRegex(
        """<pre[^>]*>(?<inner>.*?)</pre>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex PreInner();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(
        """<table\b[^>]*>.*?</table>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex TableElement();
}
