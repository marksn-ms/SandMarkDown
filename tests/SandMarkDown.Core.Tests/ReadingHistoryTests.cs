using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies back and forward through opened files.
/// </summary>
public class ReadingHistoryTests
{
    /// <summary>
    /// Opening a second file, then going back, returns the first file.
    /// </summary>
    [Fact]
    public void Back_returns_the_previous_file()
    {
        var history = new ReadingHistory();
        history.Record(@"C:\notes\a.md", null);
        history.Record(@"C:\notes\b.md", "Section");

        var place = history.GoBack();

        Assert.NotNull(place);
        Assert.EndsWith("a.md", place.Value.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Null(place.Value.Fragment);
        Assert.False(history.CanGoBack);
        Assert.True(history.CanGoForward);
    }

    /// <summary>
    /// A new file after going back drops the forward stop.
    /// </summary>
    [Fact]
    public void Record_after_back_drops_the_forward_file()
    {
        var history = new ReadingHistory();
        history.Record(@"C:\notes\a.md", null);
        history.Record(@"C:\notes\b.md", null);
        history.GoBack();
        history.Record(@"C:\notes\c.md", null);

        Assert.False(history.CanGoForward);
        Assert.EndsWith("c.md", history.Current?.Path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("a.md", history.GoBack()?.Path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Recording the current file again does not grow the list.
    /// </summary>
    [Fact]
    public void Record_same_place_is_a_no_op()
    {
        var history = new ReadingHistory();
        history.Record(@"C:\notes\a.md", "#Top");
        history.Record(@"C:\notes\a.md", "Top");

        Assert.False(history.CanGoBack);
        Assert.Equal("Top", history.Current?.Fragment);
    }

    /// <summary>
    /// The welcome page is a real stop, so back can return to a file after close.
    /// </summary>
    [Fact]
    public void Welcome_is_a_stop()
    {
        var history = new ReadingHistory();
        history.Record(@"C:\notes\a.md", null);
        history.Record(null, null);

        var back = history.GoBack();
        var forward = history.GoForward();

        Assert.EndsWith("a.md", back?.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Null(forward?.Path);
    }
}
