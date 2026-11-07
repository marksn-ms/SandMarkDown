using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SandMarkDown.Distribution;

/// <summary>
/// What a workflow should do with one version.
/// </summary>
public enum ReleaseAction
{
    /// <summary>Do not publish. The workflow succeeds.</summary>
    Skip,

    /// <summary>Create the GitHub Release.</summary>
    Publish,

    /// <summary>Stop. Do not create or replace a release.</summary>
    Reject,
}

/// <summary>
/// The outcome of comparing the project version with the previous commit and any existing tag.
/// </summary>
/// <param name="Action">Whether to publish.</param>
/// <param name="Version">The current stable version, when it could be read.</param>
/// <param name="Tag">The <c>vX.Y.Z</c> tag for <see cref="Version"/>.</param>
/// <param name="PreviousVersion">The previous stable version, when this is an increase.</param>
/// <param name="Reason">A short explanation.</param>
public sealed record ReleaseDecision(
    ReleaseAction Action,
    string? Version,
    string? Tag,
    string? PreviousVersion,
    string Reason)
{
    /// <summary>
    /// Lines the workflow can read. One <c>key=value</c> pair per line.
    /// </summary>
    /// <returns>The machine-readable form.</returns>
    public string ToOutput()
    {
        return string.Join(
            Environment.NewLine,
            $"action={Action.ToString().ToLowerInvariant()}",
            $"version={Version}",
            $"tag={Tag}",
            $"previous={PreviousVersion}",
            $"reason={Reason}");
    }
}

/// <summary>
/// Decides whether a change to the application version publishes a GitHub Release.
/// Tags use <c>vX.Y.Z</c>. An existing tag is never replaced.
/// </summary>
public static partial class ReleasePlanner
{
    /// <summary>
    /// Prefix of a published tag. The rest is the stable version.
    /// </summary>
    public const string TagPrefix = "v";

    /// <summary>
    /// A push to the default branch publishes only when the project version increased and the tag is free.
    /// A missing previous project file does not publish. That is how the distribution files themselves land.
    /// </summary>
    /// <param name="previousVersion">Version in the parent commit, or <see langword="null"/> when there is no parent project file.</param>
    /// <param name="currentVersion">Version in the commit being built.</param>
    /// <param name="tagExists">True when <c>v</c> plus <paramref name="currentVersion"/> is already a GitHub Release.</param>
    /// <returns>The decision.</returns>
    public static ReleaseDecision ForPush(string? previousVersion, string currentVersion, bool tagExists)
    {
        if (!TryParseStable(currentVersion, out var current))
        {
            return Reject(currentVersion, $"Version '{currentVersion}' must be MAJOR.MINOR.PATCH.");
        }

        if (previousVersion is null)
        {
            return Skip(current, "The previous commit has no project version, so this is not a version increase.");
        }

        if (!TryParseStable(previousVersion, out var previous))
        {
            return Reject(currentVersion, $"Previous version '{previousVersion}' must be MAJOR.MINOR.PATCH.");
        }

        if (current < previous)
        {
            return Reject(currentVersion, $"Version {currentVersion} is lower than {previousVersion}. This workflow does not publish a downgrade.");
        }

        if (current == previous)
        {
            return Skip(current, $"Version {currentVersion} is unchanged.");
        }

        if (tagExists)
        {
            return Reject(currentVersion, $"Release {Tag(currentVersion)} already exists. This workflow does not overwrite tags or releases.");
        }

        return new ReleaseDecision(
            ReleaseAction.Publish,
            currentVersion,
            Tag(currentVersion),
            previousVersion,
            $"Version increased from {previousVersion} to {currentVersion}.");
    }

    /// <summary>
    /// A manual run publishes the current version when its tag does not exist.
    /// A tag that already exists is left alone and the run succeeds.
    /// </summary>
    /// <param name="currentVersion">Version in the commit being built.</param>
    /// <param name="tagExists">True when the tag is already a GitHub Release.</param>
    /// <returns>The decision.</returns>
    public static ReleaseDecision ForManual(string currentVersion, bool tagExists)
    {
        if (!TryParseStable(currentVersion, out _))
        {
            return Reject(currentVersion, $"Version '{currentVersion}' must be MAJOR.MINOR.PATCH.");
        }

        if (tagExists)
        {
            return Skip(Version.Parse(currentVersion), $"Release {Tag(currentVersion)} already exists. This workflow does not overwrite tags or releases.");
        }

        return new ReleaseDecision(
            ReleaseAction.Publish,
            currentVersion,
            Tag(currentVersion),
            null,
            $"Manual run will publish {Tag(currentVersion)} because that tag does not exist.");
    }

    /// <summary>
    /// Reads the single <c>Version</c> element from an SDK project file.
    /// </summary>
    /// <param name="projectPath">Path to the application <c>.csproj</c>.</param>
    /// <returns>The version text.</returns>
    public static string ReadProjectVersion(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var versions = document.Descendants("Version")
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (versions.Count != 1)
        {
            throw new InvalidOperationException($"Expected one <Version> in {projectPath}, found {versions.Count}.");
        }

        return versions[0];
    }

    /// <summary>
    /// Drops feed entries whose files are not in <paramref name="directory"/>.
    /// Each GitHub Release then advertises only the packages attached to that release.
    /// </summary>
    /// <param name="feedPath">Path to <c>releases.win.json</c>.</param>
    /// <param name="directory">Directory that will be uploaded.</param>
    /// <returns>How many feed entries were removed.</returns>
    public static int FilterFeed(string feedPath, string directory)
    {
        var node = JsonNode.Parse(File.ReadAllText(feedPath))
            ?? throw new InvalidOperationException($"The feed '{feedPath}' is empty.");
        if (node is not JsonObject root)
        {
            throw new InvalidOperationException($"The feed '{feedPath}' is not a JSON object.");
        }

        var assetsName = root.Select(pair => pair.Key).FirstOrDefault(key =>
            key.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The feed '{feedPath}' has no Assets array.");
        if (root[assetsName] is not JsonArray assets)
        {
            throw new InvalidOperationException($"The feed '{feedPath}' Assets value is not an array.");
        }

        var removed = 0;
        for (var i = assets.Count - 1; i >= 0; i--)
        {
            var fileName = FileNameOf(assets[i]);
            if (fileName is null || !File.Exists(Path.Combine(directory, fileName)))
            {
                assets.RemoveAt(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            File.WriteAllText(feedPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }

        return removed;
    }

    /// <summary>
    /// True when <paramref name="text"/> is a canonical <c>MAJOR.MINOR.PATCH</c> version.
    /// </summary>
    /// <param name="text">Version text.</param>
    /// <param name="version">The parsed version.</param>
    /// <returns>True when the text is a stable three-part version.</returns>
    public static bool TryParseStable(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text) || !StableVersionPattern().IsMatch(text))
        {
            return false;
        }

        if (!Version.TryParse(text, out var parsed) || parsed.ToString(3) != text)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    /// <summary>
    /// The GitHub tag for a stable version.
    /// </summary>
    /// <param name="version">A <c>MAJOR.MINOR.PATCH</c> version.</param>
    /// <returns>The tag, such as <c>v1.0.0</c>.</returns>
    public static string Tag(string version) => TagPrefix + version;

    private static ReleaseDecision Skip(Version version, string reason)
    {
        var text = version.ToString(3);
        return new ReleaseDecision(ReleaseAction.Skip, text, Tag(text), null, reason);
    }

    private static ReleaseDecision Reject(string? version, string reason)
    {
        var stable = TryParseStable(version, out var parsed);
        var text = stable ? parsed.ToString(3) : null;
        return new ReleaseDecision(ReleaseAction.Reject, text, text is null ? null : Tag(text), null, reason);
    }

    private static string? FileNameOf(JsonNode? asset)
    {
        if (asset is not JsonObject entry)
        {
            return null;
        }

        var name = entry.Select(pair => pair.Key).FirstOrDefault(key =>
            key.Equals("FileName", StringComparison.OrdinalIgnoreCase));
        return name is null ? null : entry[name]?.GetValue<string>();
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex StableVersionPattern();
}
