using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies which command-line arguments open a file and which may check for an update.
/// </summary>
public class LaunchArgumentsTests
{
    /// <summary>
    /// A Markdown path is opened. Help flags and Velopack hooks are not file paths.
    /// </summary>
    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--help" }, null)]
    [InlineData(new[] { "--veloapp-install", "1.0.0" }, null)]
    [InlineData(new[] { "--veloapp-updated" }, null)]
    [InlineData(new[] { @"C:\notes\readme.md" }, @"C:\notes\readme.md")]
    public void File_path_skips_help_and_velopack_flags(string[] args, string? expected)
    {
        Assert.Equal(expected, LaunchArguments.FilePath(args));
    }

    /// <summary>
    /// Startup asks about an update only when the reader was not opened on a file.
    /// </summary>
    [Fact]
    public void Update_check_waits_when_a_file_was_opened()
    {
        Assert.True(LaunchArguments.ShouldCheckForUpdates([]));
        Assert.True(LaunchArguments.ShouldCheckForUpdates(["--help"]));
        Assert.True(LaunchArguments.ShouldCheckForUpdates(["--veloapp-updated"]));
        Assert.False(LaunchArguments.ShouldCheckForUpdates([@"C:\notes\readme.md"]));
    }
}
