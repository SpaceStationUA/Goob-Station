// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CustomMarkings;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.CustomMarkings;

/// <summary>A marking open in the editor: its art, and the steps that can be undone and redone.</summary>
public sealed class CustomMarkingSketch
{
    public const int MaxSteps = 64;

    /// <summary>Farther from the facing than any cursor gets while drawing on it.</summary>
    private const int MaxReach = 4096;

    public readonly CustomMarkingArt Art;

    private readonly List<CustomMarkingArt> _undo = new();
    private readonly List<CustomMarkingArt> _redo = new();
    private bool _open;

    public CustomMarkingSketch(CustomMarkingArt art)
    {
        Art = art.Clone();
    }

    /// <summary>
    /// Where the mirror stands on the standard body, whose front and back are an odd number of pixels wide: on
    /// column 15, their middle one.
    /// </summary>
    public const int DefaultMirrorAxis = 30;

    /// <summary>
    /// Whether drawing is mirrored across a vertical line through the body's middle, so what goes on one side goes
    /// on the other too.
    /// </summary>
    public bool Mirror;

    /// <summary>
    /// Where the mirror stands, as the sum of any two columns that mirror each other. Even when it stands on a
    /// column, which then mirrors to itself; odd when it stands between two.
    /// </summary>
    public int MirrorAxis = DefaultMirrorAxis;

    /// <summary>
    /// Where drawing may add to the art, by facing, x and y: the body shown and the margin around it. Null allows
    /// the whole facing. Erasing reaches everywhere, as art drawn for another body can lie out of reach of this one.
    /// </summary>
    public Func<int, int, int, bool>? Allowed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Opens a step: everything drawn until <see cref="End"/> is undone together.</summary>
    public void Begin()
    {
        if (_open)
            return;

        _open = true;
        _undo.Add(Art.Clone());
        if (_undo.Count > MaxSteps)
            _undo.RemoveAt(0);
    }

    /// <summary>Closes the open step, dropping it if nothing changed. Does nothing when no step is open.</summary>
    public void End()
    {
        if (!_open)
            return;

        _open = false;
        if (_undo[^1].Same(Art))
            _undo.RemoveAt(_undo.Count - 1);
        else
            _redo.Clear();
    }

    /// <summary>Runs one edit as a step of its own.</summary>
    public void Change(Action<CustomMarkingArt> edit)
    {
        Begin();
        edit(Art);
        End();
    }

    /// <summary>The column a column mirrors to. It may lie off the facing, where nothing is drawn.</summary>
    public int MirrorX(int x)
    {
        return MirrorAxis - x;
    }

    /// <summary>
    /// Draws a straight run of pixels on a frame, as a fast drag skips some between two mouse positions. Ends may
    /// lie outside the facing; only the part inside is drawn. While mirroring, the run is drawn on the other half
    /// as well.
    /// </summary>
    public void Line(int frame, int facing, Vector2i from, Vector2i to, Rgba32 ink)
    {
        Mirrored(from, to, (x, y) =>
        {
            if (ink.A == 0 || Allowed == null || Allowed(facing, x, y))
                Art.SetPixel(frame, facing, x, y, ink);
        });
    }

    /// <summary>
    /// Erases the body along a straight run of pixels, or brings it back, mirrored like <see cref="Line"/>. The
    /// body can only be erased where drawing is allowed; it can be brought back anywhere.
    /// </summary>
    public void EraseLine(int facing, Vector2i from, Vector2i to, bool erased)
    {
        Mirrored(from, to, (x, y) =>
        {
            if (!erased || Allowed == null || Allowed(facing, x, y))
                Art.SetErased(facing, x, y, erased);
        });
    }

    /// <summary>Fills from a pixel of a frame, and while mirroring from the pixel across the middle too.</summary>
    public void Fill(int frame, int facing, int x, int y, Rgba32 ink)
    {
        Func<int, int, bool>? reach = null;
        if (Allowed is { } allowed && ink.A != 0)
            reach = (atX, atY) => allowed(facing, atX, atY);

        Art.Fill(frame, facing, x, y, ink, reach);
        if (Mirror)
            Art.Fill(frame, facing, MirrorX(x), y, ink, reach);
    }

    private void Mirrored(Vector2i from, Vector2i to, Action<int, int> plot)
    {
        Run(from, to, plot);
        if (Mirror)
            Run(new Vector2i(MirrorX(from.X), from.Y), new Vector2i(MirrorX(to.X), to.Y), plot);
    }

    /// <summary>Calls <paramref name="plot"/> for each pixel of a facing on the straight run between two points.</summary>
    private static void Run(Vector2i from, Vector2i to, Action<int, int> plot)
    {
        from = Bound(from);
        to = Bound(to);
        var steps = Math.Max(1, Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)));
        for (var i = 0; i <= steps; i++)
        {
            var x = (int) MathF.Round(from.X + (to.X - from.X) * (i / (float) steps));
            var y = (int) MathF.Round(from.Y + (to.Y - from.Y) * (i / (float) steps));
            if (CustomMarkingArt.InFrame(x, y))
                plot(x, y);
        }
    }

    /// <summary>Keeps a point within reach, so a line to it is a bounded amount of work.</summary>
    private static Vector2i Bound(Vector2i point)
    {
        return new Vector2i(Math.Clamp(point.X, -MaxReach, MaxReach), Math.Clamp(point.Y, -MaxReach, MaxReach));
    }

    public void Undo()
    {
        Step(_undo, _redo);
    }

    public void Redo()
    {
        Step(_redo, _undo);
    }

    private void Step(List<CustomMarkingArt> from, List<CustomMarkingArt> to)
    {
        End();
        if (from.Count == 0)
            return;

        to.Add(Art.Clone());
        Art.Restore(from[^1]);
        from.RemoveAt(from.Count - 1);
    }
}
