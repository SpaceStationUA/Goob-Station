// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// One facing of a custom marking, drawn large over a body. The body is the layers of a doll, drawn here one by
/// one so the art sits at the depth its placement gives it, exactly as it will in a round.
/// </summary>
public sealed partial class CustomMarkingCanvas : Control
{
    private const int Frame = CustomMarkingRules.FrameSize;

    private static readonly Color Backdrop = Color.FromHex("#2A2D33");
    private static readonly Color BackdropAlt = Color.FromHex("#33373F");
    private static readonly Color GridLine = Color.White.WithAlpha(0.07f);
    private static readonly Color HoverLine = Color.White.WithAlpha(0.9f);
    private static readonly Color OutOfReach = Color.Black.WithAlpha(0.6f);
    private static readonly Color ReachEdge = Color.FromHex("#FFC53D");
    private static readonly Color ErasedTint = Color.FromHex("#FF5A5A").WithAlpha(0.3f);
    private static readonly Color ErasedEdge = Color.FromHex("#FF5A5A").WithAlpha(0.9f);
    private static readonly Color MirrorHoverLine = Color.White.WithAlpha(0.45f);
    private static readonly Color MirrorGuide = Color.FromHex("#4FD3FF").WithAlpha(0.8f);
    private static readonly Color MirrorColumn = Color.FromHex("#4FD3FF").WithAlpha(0.14f);

    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly CustomMarkingSystem _system;
    private readonly HashSet<int> _erasable = new();
    private readonly HashSet<int> _hair = new();
    private CustomMarkingEraseBrush? _brush;
    private EntityUid? _erasableBody;
    private int _erasableLayers;
    private EntityUid? _hairBody;
    private int _hairLayers;
    private bool _drawing;
    private bool _erasing;

    public CustomMarkingArt? Art;

    /// <summary>Which frame of <see cref="Art"/> is shown.</summary>
    public int ArtFrame;

    /// <summary>Finished art, drawn from its sprite in place of <see cref="Art"/>, frame after frame if it is animated.</summary>
    public RSI.State? ArtState;

    /// <summary>Which facing is shown and drawn on: an index into the RSI's direction order.</summary>
    public int Facing;

    /// <summary>The doll whose layers are drawn as the body, if any.</summary>
    public EntityUid? Body;

    public CustomMarkingPlacement Placement;

    public bool ShowGrid;

    /// <summary>
    /// The pixels of the body that are erased, as a <see cref="CustomMarkingErase"/> mask: the body is drawn
    /// without them. Null erases nothing.
    /// </summary>
    public byte[]? Erase;

    /// <summary>Whether to mark the pixels <see cref="Art"/> erases, for the tool that changes them.</summary>
    public bool ShowErase;

    /// <summary>
    /// Whether to leave the body's hair and facial hair undrawn, to show what is under them. Only the picture
    /// changes: the body still has its hair, and what can be drawn on is the same.
    /// </summary>
    public bool HideHair;

    /// <summary>
    /// Which body part owns each pixel on the body shown, as <see cref="CustomMarkingSections"/> maps it. Pixels
    /// no part owns are out of a marking's reach and are darkened, along with any art lying there.
    /// </summary>
    public byte[]? Sections;

    /// <summary>
    /// Where the mirror stands while drawing is mirrored, as <see cref="CustomMarkingSketch.MirrorAxis"/> gives it.
    /// Shows its line and the pixel across from the cursor.
    /// </summary>
    public int? MirrorAxis;

    /// <summary>The pixel under the cursor, outlined when the canvas takes input.</summary>
    public Vector2i? Hover { get; private set; }

    /// <summary>
    /// Raised for each pixel the cursor is pressed on or dragged to: the pixel, whether the erase button is the
    /// one held, and whether this press started the stroke.
    /// </summary>
    public event Action<Vector2i, bool, bool>? Stroke;

    public event Action? StrokeEnded;

    /// <param name="scale">Size of one art pixel. Zero for a canvas that only shows.</param>
    public CustomMarkingCanvas(int scale, bool interactive)
    {
        IoCManager.InjectDependencies(this);
        _system = _entMan.System<CustomMarkingSystem>();

        MinSize = new Vector2(Frame * scale, Frame * scale);
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;
        // One that only shows takes no clicks, so a button it sits in gets them.
        MouseFilter = interactive ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var cell = PixelSize.X / (float) Frame;

        // A checkerboard of four-pixel squares shows through wherever nothing is drawn.
        handle.DrawRect(PixelSizeBox, Backdrop);
        for (var y = 0; y < Frame; y += 4)
        {
            for (var x = y % 8; x < Frame; x += 8)
            {
                handle.DrawRect(Cells(x, y, 4, cell), BackdropAlt);
            }
        }

        if (Body is { } body && _entMan.TryGetComponent(body, out SpriteComponent? sprite))
        {
            var depth = _system.GetLayerIndex((body, sprite), Placement) ?? int.MaxValue;
            var brush = EraseBrush(body, sprite);
            var left = LeftOut(body, sprite);
            DrawBody(handle, sprite, Facing, 0, depth, PixelSize, brush, _erasable, left);
            DrawArt(handle, cell);
            DrawBody(handle, sprite, Facing, depth, int.MaxValue, PixelSize, brush, _erasable, left);
        }
        else
        {
            DrawArt(handle, cell);
        }

        if (ShowErase && Art is { } marked)
            DrawErased(handle, marked, cell);

        if (Sections is { } sections)
        {
            for (var y = 0; y < Frame; y++)
            {
                for (var x = 0; x < Frame; x++)
                {
                    if (!InReach(sections, x, y))
                        handle.DrawRect(Cells(x, y, 1, cell), OutOfReach);
                }
            }

            // On the canvas that is drawn on, a line runs along the edge of what is in reach.
            if (MouseFilter == MouseFilterMode.Stop)
                DrawReachEdge(handle, sections, cell);
        }

        if (ShowGrid)
        {
            for (var i = 1; i < Frame; i++)
            {
                var at = MathF.Round(i * cell);
                handle.DrawLine(new Vector2(at, 0), new Vector2(at, PixelSize.Y), GridLine);
                handle.DrawLine(new Vector2(0, at), new Vector2(PixelSize.X, at), GridLine);
            }
        }

        if (MirrorAxis is { } axis)
        {
            // On a column when the axis is even: that column is tinted, with the line down its middle.
            if (axis % 2 == 0)
            {
                var column = Cells(axis / 2, 0, 1, cell);
                handle.DrawRect(new UIBox2(column.Left, 0, column.Right, PixelSize.Y), MirrorColumn);
            }

            var middle = MathF.Round((axis + 1) / 2f * cell);
            handle.DrawRect(new UIBox2(middle - 1, 0, middle + 1, PixelSize.Y), MirrorGuide);
        }

        if (Hover is not { } hover)
            return;

        handle.DrawRect(Cells(hover.X, hover.Y, 1, cell), HoverLine, false);
        if (MirrorAxis is { } mirror && CustomMarkingArt.InFrame(mirror - hover.X, hover.Y))
            handle.DrawRect(Cells(mirror - hover.X, hover.Y, 1, cell), MirrorHoverLine, false);
    }

    /// <summary>
    /// What to draw the body with so the erased pixels of the facing shown are left out, or null when none are.
    /// Also lists in <see cref="_erasable"/> which of the body's layers erasing applies to.
    /// </summary>
    private CustomMarkingEraseBrush? EraseBrush(EntityUid body, SpriteComponent sprite)
    {
        if (Erase == null || !CustomMarkingErase.Any(Erase, Facing))
            return null;

        _brush ??= new CustomMarkingEraseBrush();
        if (!_brush.Set(Erase, Facing))
            return null;

        // A doll gains layers as its markings' art arrives, which moves the others.
        var layers = sprite.AllLayers.Count();
        if (_erasableBody != body || _erasableLayers != layers)
        {
            _system.GetErasable((body, sprite), _erasable);
            _erasableBody = body;
            _erasableLayers = layers;
        }

        return _brush;
    }

    /// <summary>The layers of the body that are not drawn: its hair, while that is hidden. Null when all are.</summary>
    private HashSet<int>? LeftOut(EntityUid body, SpriteComponent sprite)
    {
        if (!HideHair)
            return null;

        var layers = sprite.AllLayers.Count();
        if (_hairBody != body || _hairLayers != layers)
        {
            _system.GetHairLayers((body, sprite), _hair);
            _hairBody = body;
            _hairLayers = layers;
        }

        return _hair;
    }

    private bool InReach(byte[] sections, int x, int y)
    {
        return CustomMarkingArt.InFrame(x, y) && sections[CustomMarkingSections.Index(Facing, x, y)] != CustomMarkingSections.None;
    }

    /// <summary>Draws each side of a pixel in reach that faces one out of reach, which together outline the reach.</summary>
    private void DrawReachEdge(DrawingHandleScreen handle, byte[] sections, float cell)
    {
        var thick = MathF.Max(1f, MathF.Round(UIScale * 1.5f));
        for (var y = 0; y < Frame; y++)
        {
            for (var x = 0; x < Frame; x++)
            {
                if (!InReach(sections, x, y))
                    continue;

                var box = Cells(x, y, 1, cell);
                if (!InReach(sections, x - 1, y))
                    handle.DrawRect(new UIBox2(box.Left - thick, box.Top, box.Left, box.Bottom), ReachEdge);

                if (!InReach(sections, x + 1, y))
                    handle.DrawRect(new UIBox2(box.Right, box.Top, box.Right + thick, box.Bottom), ReachEdge);

                if (!InReach(sections, x, y - 1))
                    handle.DrawRect(new UIBox2(box.Left - thick, box.Top - thick, box.Right + thick, box.Top), ReachEdge);

                if (!InReach(sections, x, y + 1))
                    handle.DrawRect(new UIBox2(box.Left - thick, box.Bottom, box.Right + thick, box.Bottom + thick), ReachEdge);
            }
        }
    }

    /// <summary>Tints and boxes each pixel the art erases from the body, so they can be told from bare canvas.</summary>
    private void DrawErased(DrawingHandleScreen handle, CustomMarkingArt art, float cell)
    {
        for (var y = 0; y < Frame; y++)
        {
            for (var x = 0; x < Frame; x++)
            {
                if (!art.IsErased(Facing, x, y))
                    continue;

                var box = Cells(x, y, 1, cell);
                handle.DrawRect(box, ErasedTint);
                if (MouseFilter == MouseFilterMode.Stop)
                    handle.DrawRect(new UIBox2(box.Left + 1, box.Top + 1, box.Right - 1, box.Bottom - 1), ErasedEdge, false);
            }
        }
    }

    private void DrawArt(DrawingHandleScreen handle, float cell)
    {
        if (ArtState is { } state)
        {
            handle.DrawTextureRect(state.GetFrame((RsiDirection) Facing, FrameNow(state)), Cells(0, 0, Frame, cell));
            return;
        }

        if (Art == null)
            return;

        var frame = Math.Clamp(ArtFrame, 0, Art.Frames - 1);
        for (var y = 0; y < Frame; y++)
        {
            for (var x = 0; x < Frame; x++)
            {
                var pixel = Art.GetPixel(frame, Facing, x, y);
                if (pixel.A != 0)
                    handle.DrawRect(Cells(x, y, 1, cell), CustomMarkingArt.ToColor(pixel));
            }
        }
    }

    /// <summary>The frame of an animated sprite to show now, as a sprite in a round would run through them.</summary>
    private int FrameNow(RSI.State state)
    {
        if (!state.IsAnimated || state.AnimationLength <= 0)
            return 0;

        var time = (float) (_timing.RealTime.TotalSeconds % state.AnimationLength);
        for (var frame = 0; frame < state.DelayCount; frame++)
        {
            time -= state.GetDelay(frame);
            if (time < 0)
                return frame;
        }

        return state.DelayCount - 1;
    }

    /// <summary>
    /// Draws a doll's layers from one index up to another, each as its frame for a facing, with the frame scaled to
    /// <paramref name="size"/>. <see cref="CustomMarkingBodySampler"/> draws through this too, so what the colour
    /// picker reads is what the canvas shows.
    /// </summary>
    /// <param name="erase">What to draw erased layers with, or null to draw every layer whole.</param>
    /// <param name="erasable">The indices of the layers erasing applies to.</param>
    /// <param name="leftOut">The indices of layers not to draw at all.</param>
    internal static void DrawBody(
        DrawingHandleScreen handle,
        SpriteComponent sprite,
        int facing,
        int from,
        int to,
        Vector2 size,
        CustomMarkingEraseBrush? erase = null,
        HashSet<int>? erasable = null,
        HashSet<int>? leftOut = null)
    {
        var cell = size.X / Frame;
        var index = -1;
        foreach (var spriteLayer in sprite.AllLayers)
        {
            index++;
            if (index < from || index >= to
                || leftOut != null && leftOut.Contains(index)
                || spriteLayer is not SpriteComponent.Layer { Visible: true, Blank: false, CopyToShaderParameters: null } layer)
                continue;

            var texture = layer.Texture;
            if (layer.ActualRsi is { } rsi && rsi.TryGetState(layer.State, out var state))
                texture = state.GetFrame(state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : (RsiDirection) facing, 0);

            if (texture == null)
                continue;

            var drawn = (Vector2) texture.Size * cell;
            var centre = size / 2f + new Vector2(layer.Offset.X, -layer.Offset.Y) * Frame * cell;
            var erased = erase != null && erasable != null && erasable.Contains(index);
            if (erased)
                handle.UseShader(erase!.For(texture.Size));

            handle.DrawTextureRect(texture, UIBox2.FromDimensions(centre - drawn / 2f, drawn), layer.Color * sprite.Color);
            if (erased)
                handle.UseShader(null);
        }
    }

    /// <summary>The screen box of a square of art pixels, with edges rounded so neighbours never leave a seam.</summary>
    private static UIBox2 Cells(int x, int y, int span, float cell)
    {
        return new UIBox2(MathF.Round(x * cell), MathF.Round(y * cell), MathF.Round((x + span) * cell), MathF.Round((y + span) * cell));
    }

    private Vector2i PixelAt(Vector2 position)
    {
        var cell = PixelSize.X / (float) Frame;
        return new Vector2i((int) MathF.Floor(position.X / cell), (int) MathF.Floor(position.Y / cell));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        var erase = args.Function == EngineKeyFunctions.UIRightClick;
        if (MouseFilter != MouseFilterMode.Stop || args.Function != EngineKeyFunctions.UIClick && !erase)
            return;

        _drawing = true;
        _erasing = erase;
        Stroke?.Invoke(PixelAt(args.RelativePixelPosition), erase, true);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (!_drawing || args.Function != (_erasing ? EngineKeyFunctions.UIRightClick : EngineKeyFunctions.UIClick))
            return;

        _drawing = false;
        StrokeEnded?.Invoke();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        var pixel = PixelAt(args.RelativePixelPosition);
        Hover = MouseFilter == MouseFilterMode.Stop && CustomMarkingArt.InFrame(pixel.X, pixel.Y) ? pixel : null;
        if (_drawing)
            Stroke?.Invoke(pixel, _erasing, false);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Hover = null;
    }

    /// <summary>What was made to draw erased layers goes when the canvas leaves the screen; it is made again if needed.</summary>
    protected override void ExitedTree()
    {
        base.ExitedTree();
        _brush?.Dispose();
        _brush = null;
    }
}
