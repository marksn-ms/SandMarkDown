namespace SandMarkDown.Core;

/// <summary>
/// Sandmark Technologies brand constants shared by the SandMarkDown window and About box.
/// Values align with <c>docs/tokens.css</c> (<c>Style: sandmark-app</c>, <c>Theme: dark</c>).
/// </summary>
public static class Branding
{
    /// <summary>
    /// Product display name.
    /// </summary>
    public const string ProductName = "SandMarkDown";

    /// <summary>
    /// Company display name.
    /// </summary>
    public const string CompanyName = "Sandmark Technologies";

    /// <summary>
    /// Public company website.
    /// </summary>
    public const string WebsiteUrl = "https://sandmarktech.com";

    /// <summary>
    /// Window and About title pattern used by branded Sandmark apps.
    /// </summary>
    public const string ProductTitleSuffix = " · Sandmark Technologies";

    /// <summary>
    /// Copyright line for About and similar surfaces.
    /// </summary>
    public const string Copyright = "© 2026 Sandmark Technologies";

    /// <summary>
    /// One-line description shown in the About box.
    /// </summary>
    public const string Description = "A local Markdown reader.";

    /// <summary>
    /// Privacy statement covering SandMarkDown.
    /// </summary>
    public const string PrivacyUrl = "https://sandmarktech.com/privacy.html#sandmarkdown";

    /// <summary>
    /// Where to report a problem or ask a question.
    /// </summary>
    public const string SupportUrl = "https://sandmarktech.com/supportal/submit?slug=sandmarkdown";

    /// <summary>
    /// Public source repository.
    /// </summary>
    public const string SourceUrl = "https://github.com/marksn-ms/SandMarkDown";

    /// <summary>
    /// License text for SandMarkDown itself.
    /// </summary>
    public const string LicenseUrl = SourceUrl + "/blob/main/LICENSE";

    /// <summary>
    /// Short name of the SandMarkDown license.
    /// </summary>
    public const string LicenseName = "MIT License";
}
