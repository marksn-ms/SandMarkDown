namespace SandMarkDown.Core;

/// <summary>
/// HTML produced for one reading view, plus the local files that HTML may request.
/// </summary>
public sealed class RenderedDocument
{
    /// <summary>
    /// Creates a rendered document.
    /// </summary>
    /// <param name="html">Self-contained HTML page.</param>
    /// <param name="localResources">Token to absolute file path for images and local links.</param>
    /// <param name="documentPath">Source file, or <see langword="null"/> for the welcome page.</param>
    /// <param name="contentSecurityPolicy">Policy the viewer sends with the page.</param>
    /// <param name="hiddenRemoteImages">Number of web images left unloaded.</param>
    public RenderedDocument(
        string html,
        IReadOnlyDictionary<string, string> localResources,
        string? documentPath,
        string contentSecurityPolicy,
        int hiddenRemoteImages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(localResources);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSecurityPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(hiddenRemoteImages);
        Html = html;
        LocalResources = localResources;
        DocumentPath = documentPath;
        ContentSecurityPolicy = contentSecurityPolicy;
        HiddenRemoteImages = hiddenRemoteImages;
    }

    /// <summary>
    /// Self-contained HTML page.
    /// </summary>
    public string Html { get; }

    /// <summary>
    /// Map from URL token to an absolute local file path.
    /// </summary>
    public IReadOnlyDictionary<string, string> LocalResources { get; }

    /// <summary>
    /// Source Markdown path, or <see langword="null"/> for the welcome page.
    /// </summary>
    public string? DocumentPath { get; }

    /// <summary>
    /// Content Security Policy for the page, matching the policy in its meta tag.
    /// </summary>
    public string ContentSecurityPolicy { get; }

    /// <summary>
    /// Number of web images left unloaded because the reader has not asked for them.
    /// </summary>
    public int HiddenRemoteImages { get; }
}
