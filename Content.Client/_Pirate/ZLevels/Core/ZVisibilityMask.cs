// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>Reusable coarse screen mask. Cells survive only if every deck above can expose them.</summary>
internal sealed class ZVisibilityMask
{
    public const int CellSize = 64;
    public const int MaxCells = 4096;
    public const int MaxRegions = 64;
    private bool[] _visible = Array.Empty<bool>();
    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public int VisibleCount { get; private set; }
    private Vector2i _size;

    public bool Reset(Vector2i size)
    {
        Columns = Rows = VisibleCount = 0;
        if (size.X <= 0 || size.Y <= 0 || size.X > 16384 || size.Y > 16384)
            return false;

        var columns = (size.X + CellSize - 1) / CellSize;
        var rows = (size.Y + CellSize - 1) / CellSize;
        if (columns * rows > MaxCells)
            return false;

        Columns = columns;
        Rows = rows;
        _size = size;
        VisibleCount = columns * rows;
        if (_visible.Length < VisibleCount)
            _visible = new bool[VisibleCount];
        Array.Fill(_visible, true, 0, VisibleCount);
        return true;
    }

    public bool IsVisible(int x, int y) => _visible[y * Columns + x];

    public void Hide(int x, int y)
    {
        var index = y * Columns + x;
        if (!_visible[index])
            return;
        _visible[index] = false;
        VisibleCount--;
    }

    public UIBox2 CellBounds(int x, int y) => new(x * CellSize, y * CellSize,
        Math.Min((x + 1) * CellSize, _size.X), Math.Min((y + 1) * CellSize, _size.Y));

    // Merge contiguous row runs, extending vertically when the horizontal span matches.
    // Fragmented/wide masks use the ordinary render path rather than hundreds of tree queries.
    public bool TryGetRegions(List<UIBox2> regions)
    {
        regions.Clear();
        if (VisibleCount * 4 >= Columns * Rows * 3)
            return false;

        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                if (!IsVisible(x, y))
                    continue;

                var start = x;
                while (x + 1 < Columns && IsVisible(x + 1, y))
                    x++;

                var first = CellBounds(start, y);
                var last = CellBounds(x, y);
                var run = new UIBox2(first.Left, first.Top, last.Right, last.Bottom);
                var merged = false;
                for (var i = regions.Count - 1; i >= 0; i--)
                {
                    var previous = regions[i];
                    if (previous.Left != run.Left || previous.Right != run.Right || previous.Bottom != run.Top)
                        continue;
                    regions[i] = new UIBox2(previous.Left, previous.Top, previous.Right, run.Bottom);
                    merged = true;
                    break;
                }

                if (merged)
                    continue;
                if (regions.Count == MaxRegions)
                {
                    regions.Clear();
                    return false;
                }
                regions.Add(run);
            }
        }

        return true;
    }
}
