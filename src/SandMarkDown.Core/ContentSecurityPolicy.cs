using System.Security.Cryptography;

namespace SandMarkDown.Core;

/// <summary>
/// Content Security Policy for one reading page.
/// Only the viewer's own script may run, and the page may not open network connections.
/// </summary>
public sealed class ContentSecurityPolicy
{
    private ContentSecurityPolicy(string nonce, string header)
    {
        Nonce = nonce;
        Header = header;
    }

    /// <summary>
    /// Random value the viewer's own script tag carries. Script without it does not run.
    /// </summary>
    public string Nonce { get; }

    /// <summary>
    /// Policy text for a <c>Content-Security-Policy</c> header or meta tag.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Creates a policy with a fresh nonce.
    /// </summary>
    /// <param name="showRemoteImages">When <see langword="true"/>, images may load from http and https sites.</param>
    /// <returns>A policy for one page load.</returns>
    public static ContentSecurityPolicy Create(bool showRemoteImages)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var images = $"https://{MarkdownHtmlRenderer.VirtualHost} data:";
        if (showRemoteImages)
        {
            images += " https: http:";
        }

        var header = string.Join(
            "; ",
            "default-src 'none'",
            $"script-src 'nonce-{nonce}'",
            "style-src 'unsafe-inline'",
            $"img-src {images}",
            "connect-src 'none'",
            "font-src 'none'",
            "media-src 'none'",
            "object-src 'none'",
            "frame-src 'none'",
            "worker-src 'none'",
            "manifest-src 'none'",
            "base-uri 'none'",
            "form-action 'none'");
        return new ContentSecurityPolicy(nonce, header);
    }
}
