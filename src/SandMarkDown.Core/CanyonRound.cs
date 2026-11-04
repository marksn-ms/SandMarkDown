using System.Text;

namespace SandMarkDown.Core;

/// <summary>
/// One round of Canyon, the About box Easter egg. The ship stays on a fixed row
/// while the canyon scrolls up into it. Touching a wall ends the round.
/// </summary>
public sealed class CanyonRound
{
    /// <summary>
    /// Screen width in characters.
    /// </summary>
    public const int Cols = 32;

    /// <summary>
    /// Screen height in lines.
    /// </summary>
    public const int Rows = 16;

    /// <summary>
    /// Row the ship travels on.
    /// </summary>
    public const int ShipRow = 5;

    private const int MinGap = 5;
    private const int MaxGap = 14;
    private const int Opening = 12;
    private const char Wall = '\u2588';
    private const string Ship = "\"V\"";

    private readonly char[] _screen = new char[Rows * Cols];
    private readonly Random _random;
    private int _gapLeft;
    private int _gapRight;
    private int _ship;

    /// <summary>
    /// Starts a round with the ship centered in an open canyon.
    /// </summary>
    /// <param name="random">Source of canyon drift. Pass a seeded instance for a repeatable canyon.</param>
    public CanyonRound(Random? random = null)
    {
        _random = random ?? new Random();
        _gapLeft = (Cols - Opening) / 2;
        _gapRight = _gapLeft + Opening;
        _ship = _gapLeft + ((Opening - Ship.Length) / 2);
        for (var row = 0; row < Rows; row++)
        {
            Stamp(row);
        }
    }

    /// <summary>
    /// Number of rows the canyon has scrolled this round.
    /// </summary>
    public int Distance { get; private set; }

    /// <summary>
    /// Moves the ship one column.
    /// </summary>
    /// <param name="direction">Negative moves left. Positive moves right.</param>
    /// <returns><see langword="true"/> when the ship hits a wall.</returns>
    public bool Steer(int direction)
    {
        var next = _ship + Math.Sign(direction);
        if (next >= 0 && next <= Cols - Ship.Length)
        {
            _ship = next;
        }

        return Hit();
    }

    /// <summary>
    /// Scrolls the canyon up one row and stamps a new slice along the bottom.
    /// </summary>
    /// <returns><see langword="true"/> when the ship hits a wall.</returns>
    public bool Advance()
    {
        Array.Copy(_screen, Cols, _screen, 0, (Rows - 1) * Cols);
        Drift();
        Stamp(Rows - 1);
        Distance++;
        return Hit();
    }

    /// <summary>
    /// Draws the canyon with the ship painted over its row.
    /// </summary>
    /// <returns>The screen as monospaced lines.</returns>
    public string Picture()
    {
        var text = new StringBuilder(Rows * (Cols + 1));
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Cols; col++)
            {
                if (row == ShipRow && col >= _ship && col < _ship + Ship.Length)
                {
                    text.Append(Ship[col - _ship]);
                }
                else
                {
                    text.Append(_screen[(row * Cols) + col]);
                }
            }

            if (row < Rows - 1)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private bool Hit()
    {
        var start = (ShipRow * Cols) + _ship;
        for (var i = 0; i < Ship.Length; i++)
        {
            if (_screen[start + i] == Wall)
            {
                return true;
            }
        }

        return false;
    }

    // Each wall steps by at most one column. The gap stays wide enough for the ship.
    private void Drift()
    {
        _gapLeft += _random.Next(3) - 1;
        _gapRight += _random.Next(3) - 1;
        var width = _gapRight - _gapLeft;
        if (width < MinGap)
        {
            var mid = (_gapLeft + _gapRight) / 2;
            _gapLeft = mid - (MinGap / 2);
            _gapRight = _gapLeft + MinGap;
        }
        else if (width > MaxGap)
        {
            var mid = (_gapLeft + _gapRight) / 2;
            _gapLeft = mid - (MaxGap / 2);
            _gapRight = _gapLeft + MaxGap;
        }

        if (_gapLeft < 1)
        {
            _gapRight += 1 - _gapLeft;
            _gapLeft = 1;
        }

        if (_gapRight > Cols - 1)
        {
            _gapLeft -= _gapRight - (Cols - 1);
            _gapRight = Cols - 1;
        }

        if (_gapLeft < 1)
        {
            _gapLeft = 1;
        }

        if (_gapRight - _gapLeft < MinGap)
        {
            _gapRight = _gapLeft + MinGap;
        }

        if (_gapRight > Cols - 1)
        {
            _gapRight = Cols - 1;
            _gapLeft = Math.Max(1, _gapRight - MinGap);
        }
    }

    private void Stamp(int row)
    {
        var start = row * Cols;
        for (var col = 0; col < Cols; col++)
        {
            _screen[start + col] = col >= _gapLeft && col < _gapRight ? ' ' : Wall;
        }
    }
}
