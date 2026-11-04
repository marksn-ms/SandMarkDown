using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the zoom setting file.
/// </summary>
public class ZoomPreferencesTests
{
    /// <summary>
    /// A missing file means actual size.
    /// </summary>
    [Fact]
    public void Load_missing_file_is_actual_size()
    {
        var path = Path.Combine(Path.GetTempPath(), "sandmarkdown-zoom", Guid.NewGuid().ToString("N"), "zoom.json");
        Assert.Equal(ZoomMath.Actual, ZoomPreferences.Load(path));
    }

    /// <summary>
    /// A saved factor round-trips.
    /// </summary>
    [Fact]
    public void Save_then_load_round_trips()
    {
        var path = TempFile();
        ZoomPreferences.Save(path, 1.3);
        Assert.Equal(1.3, ZoomPreferences.Load(path));
    }

    /// <summary>
    /// Garbage and out-of-range files fall back to actual size.
    /// </summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "zoom": 9 }""")]
    public void Load_rejects_unusable_files(string contents)
    {
        var path = TempFile();
        File.WriteAllText(path, contents);
        Assert.Equal(ZoomMath.Actual, ZoomPreferences.Load(path));
    }

    private static string TempFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sandmarkdown-zoom", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "zoom.json");
    }
}
