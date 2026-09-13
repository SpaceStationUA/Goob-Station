// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Fullscreen dissolve from a static "before" frame (splash/lobby art) into the
///     live game, hiding the world streaming glitches between join and first stable frames.
/// </summary>
public sealed class RoundWipeOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "PirateRoundWipe";

    private readonly ShaderInstance _shader;
    private readonly Texture _art;
    private readonly float _seed;
    private readonly float _maskMode;

    // cover runs from JoinGame until the player attaches (plus a pad) — bounded by
    // MaxCover as a failsafe against stuck loads
    private static readonly TimeSpan AttachPad = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxCover = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromMilliseconds(1400);
    private static readonly TimeSpan LingerTime = TimeSpan.FromMilliseconds(150);
    private const float CoveredProgress = 0.0f;

    private TimeSpan _coverElapsed;
    private TimeSpan _releaseElapsed;
    private bool _attached;

    private WipePhase _phase = WipePhase.Cover;

    private enum WipePhase { Cover, Release, Done }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public RoundWipeOverlay(Texture art, float seed, int maskMode)
    {
        IoCManager.InjectDependencies(this);
        _art = art;
        _seed = seed;
        _maskMode = maskMode;
        _shader = IoCManager.Resolve<IPrototypeManager>().Index(Shader).InstanceUnique();
    }

    public int MaskMode => (int) _maskMode;

    public void MarkAttached()
    {
        if (_phase == WipePhase.Cover)
        {
            _phase = WipePhase.Release;
            _releaseElapsed = TimeSpan.Zero;
        }
    }

    public void Release()
    {
        if (_phase == WipePhase.Cover)
            MarkAttached();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        switch (_phase)
        {
            case WipePhase.Cover:
                _coverElapsed += TimeSpan.FromSeconds(args.DeltaSeconds);
                // failsafe: release even without an attach if the cover runs too long
                if (_coverElapsed >= MaxCover)
                    _phase = WipePhase.Release;
                break;

            case WipePhase.Release:
                _releaseElapsed += TimeSpan.FromSeconds(args.DeltaSeconds);
                // if a wipe still covers after failed attach, start releasing anyway
                // once the world had its chance (handled by MaxCover above)
                if (_releaseElapsed >= ReleaseTime)
                    _phase = WipePhase.Done;
                break;

            case WipePhase.Done:
                _releaseElapsed += TimeSpan.FromSeconds(args.DeltaSeconds);
                if (_releaseElapsed >= LingerTime)
                    _overlayMan.RemoveOverlay(this);
                break;
        }
    }

    [Dependency] private readonly IOverlayManager _overlayMan = default!;


    protected override void Draw(in OverlayDrawArgs args)
    {
        var progress = _phase switch
        {
            WipePhase.Cover => CoveredProgress,
            WipePhase.Release => (float)(_releaseElapsed / ReleaseTime),
            _ => 1f,
        };

        // art cover-fit: map screen uv into the art's texture space
        var screen = (Vector2) args.Viewport.Size;
        var artSize = _art != null ? (Vector2) _art.Size : Vector2.One;
        Vector2 scale, offset;
        var screenAspect = screen.X / screen.Y;
        var artAspect = artSize.X / artSize.Y;
        if (artAspect > screenAspect)
        {
            // art is wider: crop horizontally
            var visible = screenAspect / artAspect;
            scale = new Vector2(visible, 1f);
            offset = new Vector2((1f - visible) / 2f, 0f);
        }
        else
        {
            var visible = artAspect / screenAspect;
            scale = new Vector2(1f, visible);
            offset = new Vector2(0f, (1f - visible) / 2f);
        }
        // shader sampling: artUv = (uv - 0.5) * scale + 0.5, so pre-invert
        scale = new Vector2(1f / scale.X, 1f / scale.Y);

        if (ScreenTexture != null)
            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        if (_art != null)
            _shader.SetParameter("artTexture", _art);
        _shader.SetParameter("progress", progress);
        _shader.SetParameter("maskMode", _maskMode);
        _shader.SetParameter("seed", _seed);
        _shader.SetParameter("artScale", scale);
        _shader.SetParameter("artOffset", offset);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
