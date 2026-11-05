using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the Canyon Easter egg.
/// </summary>
public class CanyonRoundTests
{
    /// <summary>
    /// A new round draws a full screen with the ship in open space.
    /// </summary>
    [Fact]
    public void New_round_draws_the_ship_in_the_open()
    {
        var round = new CanyonRound(new Random(1));
        var lines = round.Picture().Split('\n');

        Assert.Equal(CanyonRound.Rows, lines.Length);
        Assert.All(lines, line => Assert.Equal(CanyonRound.Cols, line.Length));
        Assert.Contains("\"V\"", lines[CanyonRound.ShipRow], StringComparison.Ordinal);
    }

    /// <summary>
    /// Steering into the wall ends the round.
    /// </summary>
    [Fact]
    public void Steering_into_the_wall_ends_the_round()
    {
        var round = new CanyonRound(new Random(1));
        var hit = false;
        for (var i = 0; i < CanyonRound.Cols && !hit; i++)
        {
            hit = round.Steer(-1);
        }

        Assert.True(hit);
    }

    /// <summary>
    /// The gap always leaves room for the ship, so a careful pilot can keep going.
    /// </summary>
    [Fact]
    public void Canyon_always_leaves_a_gap_wide_enough_for_the_ship()
    {
        var round = new CanyonRound(new Random(7));
        for (var step = 0; step < 500; step++)
        {
            round.Advance();
            var bottom = round.Picture().Split('\n')[CanyonRound.Rows - 1];
            Assert.Contains("     ", bottom, StringComparison.Ordinal);
        }

        Assert.Equal(500, round.Distance);
    }

    /// <summary>
    /// The same seed draws the same canyon.
    /// </summary>
    [Fact]
    public void Seeded_rounds_repeat()
    {
        var first = new CanyonRound(new Random(42));
        var second = new CanyonRound(new Random(42));
        for (var step = 0; step < 20; step++)
        {
            first.Advance();
            second.Advance();
        }

        Assert.Equal(first.Picture(), second.Picture());
    }
}
