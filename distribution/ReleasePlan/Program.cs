using SandMarkDown.Distribution;

// Decides whether this commit publishes a GitHub Release. No secrets are required.
if (args.Length == 0 || args[0] is "-h" or "--help" or "/?")
{
    Console.WriteLine(
        """
        ReleasePlan decides whether a SandMarkDown version change publishes a GitHub Release.

        No secrets are required. The tool does not create tags or releases.

        Usage:
          ReleasePlan plan --current <project.csproj> [--previous <project.csproj>] [--tag-exists]
          ReleasePlan manual --current <project.csproj> [--tag-exists]
          ReleasePlan version --current <project.csproj>
          ReleasePlan filter-feed --feed <releases.win.json> --dir <upload-directory>
          ReleasePlan --help

        plan     A push publishes only when <Version> increased and the vX.Y.Z tag is free.
        manual   A manual run publishes the current version when its tag does not exist.
        version  Prints the project version.
        filter-feed
                 Removes feed entries whose files are not in the upload directory.

        Example:
          dotnet run --project distribution/ReleasePlan -- plan --current src/SandMarkDown/SandMarkDown.csproj --previous previous.csproj
        """);
    return 0;
}

try
{
    var command = args[0];
    var options = ReadOptions(args.Skip(1).ToArray());
    switch (command)
    {
        case "version":
            Console.WriteLine(ReleasePlanner.ReadProjectVersion(Required(options, "current")));
            return 0;
        case "plan":
            return Write(Plan(options));
        case "manual":
            return Write(Manual(options));
        case "filter-feed":
            var removed = ReleasePlanner.FilterFeed(Required(options, "feed"), Required(options, "dir"));
            Console.WriteLine($"removed={removed}");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown command '{command}'. Use --help.");
            return 1;
    }
}
catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int Write(ReleaseDecision decision)
{
    Console.WriteLine(decision.ToOutput());
    if (decision.Action == ReleaseAction.Reject)
    {
        Console.Error.WriteLine(decision.Reason);
        return 1;
    }

    return 0;
}

static ReleaseDecision Plan(Dictionary<string, string?> options)
{
    var current = ReleasePlanner.ReadProjectVersion(Required(options, "current"));
    string? previous = null;
    if (options.TryGetValue("previous", out var previousPath) && !string.IsNullOrWhiteSpace(previousPath))
    {
        previous = ReleasePlanner.ReadProjectVersion(previousPath);
    }

    return ReleasePlanner.ForPush(previous, current, options.ContainsKey("tag-exists"));
}

static ReleaseDecision Manual(Dictionary<string, string?> options)
{
    var current = ReleasePlanner.ReadProjectVersion(Required(options, "current"));
    return ReleasePlanner.ForManual(current, options.ContainsKey("tag-exists"));
}

static Dictionary<string, string?> ReadOptions(string[] args)
{
    var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument '{arg}'.");
        }

        var name = arg[2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            options[name] = args[++i];
        }
        else
        {
            options[name] = null;
        }
    }

    return options;
}

static string Required(Dictionary<string, string?> options, string name)
{
    if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing --{name}.");
    }

    return value;
}
