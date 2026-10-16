// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// Reads the colours of the body shown under the art. While <see cref="Active"/>, the doll's layers for one facing are
/// drawn at one texel per art pixel into a small render target each frame and copied back, through the same code
/// the canvas draws with. It sits at the UI root rather than in the editor because a window clips its content, and
/// that clip, set for the screen, would also cut a render target drawn from inside it.
/// </summary>
public sealed partial class CustomMarkingBodySampler : Control
{
    private const int Frame = CustomMarkingRules.FrameSize;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly Rgba32[] _pixels = new Rgba32[Frame * Frame];
    private readonly HashSet<int> _erasable = new();
    private readonly HashSet<int> _hair = new();
    private CustomMarkingEraseBrush? _brush;
    private IRenderTexture? _target;
    private EntityUid? _sampledBody;
    private int _sampledFacing = -1;

    /// <summary>The doll to read and which of its facings.</summary>
    public EntityUid? Body;

    public int Facing;

    /// <summary>The pixels of the body that are erased, which it is read without, as the canvas shows it.</summary>
    public byte[]? Erase;

    /// <summary>Whether the body is read without its hair and facial hair, as the canvas shows it then.</summary>
    public bool HideHair;

    /// <summary>Whether to keep the sample fresh. Off, nothing is drawn or read.</summary>
    public bool Active;

    public CustomMarkingBodySampler()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>The colour of the body at an art pixel, once a sample of the current body and facing has arrived and if the body is drawn there.</summary>
    public bool TryGetColor(int x, int y, out Color color)
    {
        color = default;
        if (_sampledBody == null || _sampledBody != Body || _sampledFacing != Facing || !CustomMarkingArt.InFrame(x, y))
            return false;

        return CustomMarkingSampling.TryStraightColor(_pixels[y * Frame + x], out color);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!Active || Body is not { } body || !_entMan.TryGetComponent(body, out SpriteComponent? sprite))
            return;

        _target ??= _clyde.CreateRenderTarget(new Vector2i(Frame, Frame),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "wf-custom-marking-sample");

        var facing = Facing;
        CustomMarkingEraseBrush? brush = null;
        if (Erase != null && CustomMarkingErase.Any(Erase, facing))
        {
            _brush ??= new CustomMarkingEraseBrush();
            if (_brush.Set(Erase, facing))
            {
                brush = _brush;
                _entMan.System<CustomMarkingSystem>().GetErasable((body, sprite), _erasable);
            }
        }

        HashSet<int>? leftOut = null;
        if (HideHair)
        {
            _entMan.System<CustomMarkingSystem>().GetHairLayers((body, sprite), _hair);
            leftOut = _hair;
        }

        handle.RenderInRenderTarget(_target, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            CustomMarkingCanvas.DrawBody(handle, sprite, facing, 0, int.MaxValue, new Vector2(Frame, Frame), brush, _erasable, leftOut);
        }, Color.Transparent);

        _target.CopyPixelsToMemory<Rgba32>(image =>
        {
            using (image)
            {
                // The sandbox doesn't allow reading an image through its indexer.
                var copied = image.GetPixelSpan();
                if (copied.Length != _pixels.Length)
                    return;

                copied.CopyTo(_pixels);
            }

            _sampledBody = body;
            _sampledFacing = facing;
        });
    }

    /// <summary>Taken out of the tree when the editor closes, which is when the render target goes.</summary>
    protected override void ExitedTree()
    {
        base.ExitedTree();
        _target?.Dispose();
        _target = null;
        _brush?.Dispose();
        _brush = null;
        _sampledBody = null;
    }
}
