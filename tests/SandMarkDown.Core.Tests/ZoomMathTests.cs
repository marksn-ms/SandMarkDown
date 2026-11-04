using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies reading-zoom steps stay inside the allowed range.
/// </summary>
public class ZoomMathTests
{
    /// <summary>
    /// One zoom-in step adds a tenth and does not pass the maximum.
    /// </summary>
    [Fact]
    public void Zoom_in_steps_and_clamps()
    {
        Assert.Equal(1.1, ZoomMath.ZoomIn(ZoomMath.Actual));
        Assert.Equal(ZoomMath.Maximum, ZoomMath.ZoomIn(ZoomMath.Maximum));
    }

    /// <summary>
    /// One zoom-out step subtracts a tenth and does not pass the minimum.
    /// </summary>
    [Fact]
    public void Zoom_out_steps_and_clamps()
    {
        Assert.Equal(0.9, ZoomMath.ZoomOut(ZoomMath.Actual));
        Assert.Equal(ZoomMath.Minimum, ZoomMath.ZoomOut(ZoomMath.Minimum));
    }

    /// <summary>
    /// Actual size is labeled 100 percent.
    /// </summary>
    [Fact]
    public void Format_percent_uses_whole_numbers()
    {
        Assert.Equal("100%", ZoomMath.FormatPercent(ZoomMath.Actual));
        Assert.Equal("50%", ZoomMath.FormatPercent(ZoomMath.Minimum));
    }
}
