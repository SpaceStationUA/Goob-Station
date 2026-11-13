// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.Shuttles.Warp;
using Content.Client.Graphics;
using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared._Pirate.Light;
using Content.Shared.Stealth.Components;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Pirate.Light;

// Additive and subtractive blends must not draw between floor capture and composite.

public enum SpriteAmbientOcclusionCapture : byte
{
    Floor,
    BeforeMobs,
    AfterMobs,
}

public sealed class SpriteAmbientOcclusionResources : IDisposable
{
    public const int CaptureCount = 3;
    public const int AllCaptured = (1 << CaptureCount) - 1;

    public readonly IRenderTexture?[] Captures = new IRenderTexture?[CaptureCount];
    public IRenderTexture? Mask;
    // Half-size targets reduce blur work to one quarter of the full-size pixel count.
    public IRenderTexture? MaskHalf;
    public IRenderTexture? MaskHalfBlur;
    public IRenderTexture? MaskBlur;
    public IRenderTexture? Exclude;
    public IRenderTexture? OtherFlat;
    public IRenderTexture? OtherUpright;
    public IRenderTexture? ForceFlat;

    // Two targets per size for running a redrawn sprite through its post shaders. The size is the engine's pool
    // bucket (a power of two): the post shaders read UV, which spans the whole texture.
    private readonly Dictionary<Vector2i, (IRenderTexture A, IRenderTexture B)> _postShaderTargets = new();
    private const int MaxPostShaderSizes = 16;

    // Prevents reuse of stale captures.
    public int Captured;

    public void Dispose()
    {
        foreach (var capture in Captures)
        {
            capture?.Dispose();
        }

        Mask?.Dispose();
        MaskHalf?.Dispose();
        MaskHalfBlur?.Dispose();
        MaskBlur?.Dispose();
        Exclude?.Dispose();
        OtherFlat?.Dispose();
        OtherUpright?.Dispose();
        ForceFlat?.Dispose();
        ClearPostShaderTargets();
    }

    // Queued draws can still sample these targets; trim them before the next pass.
    public (IRenderTexture A, IRenderTexture B) GetPostShaderTargets(IClyde clyde, Vector2i size)
    {
        if (_postShaderTargets.TryGetValue(size, out var targets))
            return targets;

        var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
        targets = (clyde.CreateRenderTarget(size, format, name: "pirate-ao-post-a"),
            clyde.CreateRenderTarget(size, format, name: "pirate-ao-post-b"));
        _postShaderTargets[size] = targets;
        return targets;
    }

    // Dispose only before this frame draws: queued draws can sample deleted textures.
    public void TrimPostShaderTargets()
    {
        // Rotation and zoom can give many sizes: keep the cache small.
        if (_postShaderTargets.Count > MaxPostShaderSizes)
            ClearPostShaderTargets();
    }

    private void ClearPostShaderTargets()
    {
        foreach (var (a, b) in _postShaderTargets.Values)
        {
            a.Dispose();
            b.Dispose();
        }

        _postShaderTargets.Clear();
    }

    public static void EnsureTarget(IClyde clyde, ref IRenderTexture? target, Vector2i size, string name,
        bool filter = false, RenderTargetColorFormat format = RenderTargetColorFormat.Rgba8Srgb)
    {
        if (target?.Texture.Size == size)
            return;

        target?.Dispose();
        target = clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(format),
            filter ? new TextureSampleParameters { Filter = true } : null,
            name: name);
    }

    public static void DrawFullTarget(DrawingHandleWorld handle, Vector2i size)
    {
        handle.SetTransform(Matrix3x2.Identity);
        handle.DrawRect(new Box2(-1, -1, size.X + 1, size.Y + 1), Color.White);
    }
}

// OverlayManager silently rejects duplicate overlay types.
public abstract class SpriteAmbientOcclusionCaptureOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CopyShader = "PirateAmbientOcclusionCopy";
    private static readonly ProtoId<ShaderPrototype> ClearAlphaShader = "PirateAmbientOcclusionClearAlpha";

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly OverlayResourceCache<SpriteAmbientOcclusionResources> _resources;
    private readonly ShaderInstance _copy;
    private readonly ShaderInstance? _clearAlpha;
    private readonly int _slot;
    private readonly string _targetName;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    // Avoid the engine's viewport copy; RenderInRenderTarget flushes draws before sampling the viewport target.

    // Clearing alpha preserves coverage even when sprites match the background colour.
    protected SpriteAmbientOcclusionCaptureOverlay(
        OverlayResourceCache<SpriteAmbientOcclusionResources> resources,
        SpriteAmbientOcclusionCapture capture,
        bool clearAlpha)
    {
        IoCManager.InjectDependencies(this);
        _resources = resources;
        _copy = _proto.Index(CopyShader).InstanceUnique();
        _clearAlpha = clearAlpha ? _proto.Index(ClearAlphaShader).InstanceUnique() : null;
        _slot = (int) capture;
        _targetName = $"pirate-ao-{capture}";

        // Each capture is drawn right before the first sprite with this draw depth.
        ZIndex = capture switch
        {
            // Everything below dead mobs (floor objects, decals, puddles, pipes, floor effects) is "floor": it
            // gets the shadow and casts none.
            SpriteAmbientOcclusionCapture.Floor => (int) DrawDepth.DeadMobs,
            SpriteAmbientOcclusionCapture.BeforeMobs => (int) DrawDepth.Mobs,
            // Mobs and OverMobs are upright. Doors and up are flat again.
            SpriteAmbientOcclusionCapture.AfterMobs => (int) DrawDepth.Doors,
            _ => throw new ArgumentOutOfRangeException(nameof(capture)),
        };
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.MapId != MapId.Nullspace && args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var res = _resources.GetForViewport(args.Viewport, static _ => new SpriteAmbientOcclusionResources());
        var viewportTarget = args.Viewport.RenderTarget;
        var size = viewportTarget.Size;
        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.Captures[_slot], size, _targetName);
        var target = res.Captures[_slot]!;

        var handle = args.WorldHandle;
        _copy.SetParameter("SCREEN_TEXTURE", viewportTarget.Texture);
        handle.RenderInRenderTarget(target, () =>
        {
            handle.UseShader(_copy);
            SpriteAmbientOcclusionResources.DrawFullTarget(handle, size);
        }, Color.Transparent);

        res.Captured |= 1 << _slot;

        if (_clearAlpha == null)
            return;

        // RenderInRenderTarget restored the viewport, so sample the capture explicitly.
        _clearAlpha.SetParameter("SCREEN_TEXTURE", target.Texture);
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_clearAlpha);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _copy.Dispose();
        _clearAlpha?.Dispose();
        base.DisposeBehavior();
    }
}

public sealed class SpriteAmbientOcclusionFloorCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    : SpriteAmbientOcclusionCaptureOverlay(resources, SpriteAmbientOcclusionCapture.Floor, clearAlpha: true);

public sealed class SpriteAmbientOcclusionBeforeMobsCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    : SpriteAmbientOcclusionCaptureOverlay(resources, SpriteAmbientOcclusionCapture.BeforeMobs, clearAlpha: false);

// Without the second clear, doors over opaque mobs lose their shadows.
public sealed class SpriteAmbientOcclusionAfterMobsCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    : SpriteAmbientOcclusionCaptureOverlay(resources, SpriteAmbientOcclusionCapture.AfterMobs, clearAlpha: true);

public sealed class SpriteAmbientOcclusionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> MaskShader = "PirateAmbientOcclusionMask";
    private static readonly ProtoId<ShaderPrototype> SimpleMaskShader = "PirateAmbientOcclusionMaskSimple";
    private static readonly ProtoId<ShaderPrototype> DownsampleShader = "PirateAmbientOcclusionDownsample";

    private const float HalfResMinRenderPerWorldPixel = 0.999f;
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "PirateAmbientOcclusionComposite";

    // Culling margin for stealthed sprites: covers the dilation below and rounding.
    private const float ExcludeMargin = 4f / EyeManager.PixelsPerMeter;

    // The silhouette is drawn at these render pixel offsets (a 1 px dilation), so rounding differences against the
    // engine's own draw of the sprite cannot leave a casting ring around it.
    private static readonly Vector2[] ExcludeOffsets =
    [
        Vector2.Zero, new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
    ];

    private const float ShadowOffset = 2f;

    private const float ShadowBlur = 12f;

    private static readonly Color ShadowColor = Color.FromHex("#04080FAA");

    // The mask shader supports at most 32 fade steps.
    private const float UprightFade = 16f;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly OverlayResourceCache<SpriteAmbientOcclusionResources> _resources;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _simpleMask;
    private readonly ShaderInstance _downsample;
    private readonly ShaderInstance _composite;
    // Fixture bounds are exact; the sprite strip can be a pixel wider.
    private const float ForceFlatMargin = 1f / EyeManager.PixelsPerMeter;

    // No dilation for the other casters: their coverage must match the frame.
    private static readonly Vector2[] NoOffsets = [Vector2.Zero];

    // Clyde.PostShadeScale: the renderer's post shader target is this many times the sprite's screen bounds.
    private const float PostShadeScale = 1.25f;

    private readonly List<(ShaderInstance Shader, bool ScreenUv)> _postShaders = new();

    // Content cannot inspect shader sources; list each post shader that reads SCREEN_UV.
    private static readonly HashSet<string> ScreenUvPostShaders =
    [
        "slasher-boogeyman", // Goobstation BoogeymanShadowSystem.PostShaderId: alpha from the light map at SCREEN_UV
    ];

    private readonly record struct SilhouetteView(
        Matrix3x2 WorldToLocal,
        Vector2 DrawScale,
        Angle EyeRotation,
        Vector2i TargetSize);

    private DrawingHandleScreen? _screenHandle;
    private readonly List<Box2> _stealthBoxes = new();
    private readonly HashSet<EntityUid> _stealthUids = new();
    private readonly List<Silhouette> _stealthed = new();
    private readonly List<Silhouette> _otherFlat = new();
    private readonly List<Silhouette> _otherUpright = new();
    private readonly List<Entity<SpriteComponent, TransformComponent>> _treeResults = new();
    private readonly Dictionary<EntityUid, Vector2> _gridSnaps = new();
    private readonly HashSet<EntityUid> _queriedSprites = new();
    private readonly List<UIBox2> _hazeQuads = new();

    private readonly record struct StealthTextures(Texture Exclude, Texture OtherFlat, Texture OtherUpright);
    private readonly List<Box2> _flatBoxes = new();
    private readonly HashSet<Entity<AmbientOcclusionFlatComponent>> _flatEntities = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    public SpriteAmbientOcclusionOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    {
        IoCManager.InjectDependencies(this);
        _resources = resources;
        _mask = _proto.Index(MaskShader).InstanceUnique();
        _simpleMask = _proto.Index(SimpleMaskShader).InstanceUnique();
        _downsample = _proto.Index(DownsampleShader).InstanceUnique();
        _composite = _proto.Index(CompositeShader).InstanceUnique();

        // Drawn before Effects (gas overlay, fire, melee swings) and ghosts, which must not cast a shadow.
        // Kudzu (Overdoors) is drawn after this too, so it casts none.
        ZIndex = (int) DrawDepth.Effects - 1;

        _ui.OnPostDrawUIRoot += OnPostDrawUIRoot;
    }

    private void OnPostDrawUIRoot(PostDrawUIRootEventArgs args)
    {
        _screenHandle = args.DrawingHandle;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.MapId != MapId.Nullspace && args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (ScreenTexture == null || eye == null)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new SpriteAmbientOcclusionResources());
        var captured = res.Captured;
        res.Captured = 0;
        if (captured != SpriteAmbientOcclusionResources.AllCaptured)
            return;

        var size = args.Viewport.RenderTarget.Size;
        var floor = res.Captures[(int) SpriteAmbientOcclusionCapture.Floor]!;
        var beforeMobs = res.Captures[(int) SpriteAmbientOcclusionCapture.BeforeMobs]!;
        var afterMobs = res.Captures[(int) SpriteAmbientOcclusionCapture.AfterMobs]!;
        if (floor.Texture.Size != size || beforeMobs.Texture.Size != size || afterMobs.Texture.Size != size)
            return;

        // Render pixels per world pixel, so the offsets stay in world pixels at any zoom or render scale.
        var renderPerWorldPixel = args.Viewport.RenderScale.Y * eye.Scale.Y;
        var pixelSize = Vector2.One / size;

        // Keep the blur full-size when it is under 2 render pixels; half-size blur distorts it at zoomed-out scales.
        var halfRes = renderPerWorldPixel >= HalfResMinRenderPerWorldPixel;
        var halfSize = new Vector2i((size.X + 1) / 2, (size.Y + 1) / 2);
        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.Mask, size, "pirate-ao-mask", filter: true,
            format: RenderTargetColorFormat.R8);
        if (halfRes)
        {
            SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.MaskHalf, halfSize, "pirate-ao-mask-half",
                filter: true, format: RenderTargetColorFormat.R8);
            SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.MaskHalfBlur, halfSize,
                "pirate-ao-mask-half-blur", filter: true, format: RenderTargetColorFormat.R8);
        }
        else
        {
            SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.MaskBlur, size, "pirate-ao-mask-blur",
                filter: true, format: RenderTargetColorFormat.R8);
        }

        var handle = args.WorldHandle;
        var stealth = DrawStealth(args, res, eye, size);
        var forceFlat = DrawForceFlat(args, res, eye, size);

        // Separate programs keep exclusion branches and samplers out of the common path.
        var hasExclusions = stealth.Exclude != Texture.Transparent;
        var mask = hasExclusions ? _mask : _simpleMask;
        mask.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        mask.SetParameter("BEFORE_MOBS_TEXTURE", beforeMobs.Texture);
        mask.SetParameter("AFTER_MOBS_TEXTURE", afterMobs.Texture);
        if (hasExclusions)
        {
            mask.SetParameter("EXCLUDE_TEXTURE", stealth.Exclude);
            mask.SetParameter("OTHER_FLAT_TEXTURE", stealth.OtherFlat);
            mask.SetParameter("OTHER_UPRIGHT_TEXTURE", stealth.OtherUpright);
        }
        mask.SetParameter("FLAT_TEXTURE", forceFlat);
        mask.SetParameter("STEP", new Vector2(0f, renderPerWorldPixel * pixelSize.Y));
        mask.SetParameter("FADE", UprightFade);
        handle.RenderInRenderTarget(res.Mask!, () =>
        {
            handle.UseShader(mask);
            SpriteAmbientOcclusionResources.DrawFullTarget(handle, size);
        }, Color.Transparent);

        IRenderTexture shadowMask;
        Vector2 maskUvScale;
        if (halfRes)
        {
            // Sample each 2x2 block center; filtered input averages its pixels and preserves alignment for odd sizes.
            _downsample.SetParameter("SOURCE_TEXTURE", res.Mask!.Texture);
            _downsample.SetParameter("SOURCE_PIXEL_SIZE", pixelSize);
            handle.RenderInRenderTarget(res.MaskHalf!, () =>
            {
                handle.UseShader(_downsample);
                SpriteAmbientOcclusionResources.DrawFullTarget(handle, halfSize);
            }, Color.Transparent);

            // Blur radius uses viewport dimensions, so it stays constant for half-size targets.
            BlurLinearMask(handle, args.Viewport, eye, res.MaskHalf!, res.MaskHalfBlur!);
            shadowMask = res.MaskHalf!;
            // Map full pixels to half-resolution texels using the downsample's 2x2 block centers.
            maskUvScale = Vector2.One / (halfSize * 2);
        }
        else
        {
            BlurLinearMask(handle, args.Viewport, eye, res.Mask!, res.MaskBlur!);
            shadowMask = res.Mask!;
            maskUvScale = pixelSize;
        }

        _composite.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _composite.SetParameter("FLOOR_TEXTURE", floor.Texture);
        _composite.SetParameter("AFTER_MOBS_TEXTURE", afterMobs.Texture);
        _composite.SetParameter("MASK_TEXTURE", shadowMask.Texture);
        _composite.SetParameter("MASK_UV_SCALE", maskUvScale);
        _composite.SetParameter("EXCLUDE_TEXTURE", stealth.Exclude);
        _composite.SetParameter("OTHER_FLAT_TEXTURE", stealth.OtherFlat);
        _composite.SetParameter("OTHER_UPRIGHT_TEXTURE", stealth.OtherUpright);
        _composite.SetParameter("HAS_EXCLUSIONS", hasExclusions);
        _composite.SetParameter("SHIFT", new Vector2(0f, ShadowOffset * renderPerWorldPixel));
        _composite.SetParameter("SHADOW", new Vector4(ShadowColor.R, ShadowColor.G, ShadowColor.B, ShadowColor.A));

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_composite);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private readonly record struct Silhouette(
        EntityUid Uid,
        SpriteComponent Sprite,
        Vector2 Pixel,
        Angle Rotation,
        Vector2 WorldPosition,
        Vector2 Snap);

    // Stealth writes opaque alpha, so overlapping casters must be redrawn.
    private StealthTextures DrawStealth(in OverlayDrawArgs args, SpriteAmbientOcclusionResources res, IEye eye, Vector2i size)
    {
        var blank = new StealthTextures(Texture.Transparent, Texture.Transparent, Texture.Transparent);
        if (_screenHandle is not { Disposed: false } screen)
            return blank;

        // Before any draw of this frame can sample a cached post shader target.
        res.TrimPostShaderTargets();

        _stealthBoxes.Clear();
        _stealthed.Clear();
        _gridSnaps.Clear();
        var sprites = _entManager.System<SpriteSystem>();
        var xforms = _entManager.System<SharedTransformSystem>();
        var bounds = args.WorldAABB;

        // The targets below have the viewport's size, so the viewport target gives the same matrix. It is needed
        // before the targets exist: the haze lookup boxes come from screen quads.
        var worldToLocal = args.Viewport.RenderTarget.GetWorldToLocalMatrix(eye, TargetScale(args.Viewport, size));
        var origin = Vector2.Transform(Vector2.Zero, worldToLocal);
        var renderPerMeter = (Vector2.Transform(Vector2.UnitX, worldToLocal) - origin).Length();
        var drawScale = new Vector2(renderPerMeter / EyeManager.PixelsPerMeter);
        var view = new SilhouetteView(worldToLocal, drawScale, eye.Rotation, size);
        Matrix3x2.Invert(worldToLocal, out var localToWorld);

        var query = _entManager.EntityQueryEnumerator<StealthComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stealth, out var sprite, out var xform))
        {
            if (!stealth.Enabled || !sprite.Visible || xform.MapID != args.MapId)
                continue;

            // Culling only: a box that holds the sprite at any rotation.
            var local = sprites.GetLocalBounds((uid, sprite));
            var radius = MathF.Max(
                MathF.Max(MathF.Abs(local.Left), MathF.Abs(local.Right)),
                MathF.Max(MathF.Abs(local.Bottom), MathF.Abs(local.Top)));
            radius = radius * MathF.Sqrt(2f) + ExcludeMargin;

            var (position, _) = xforms.GetWorldPositionRotation(xform);
            var box = Box2.CenteredAround(position, new Vector2(radius * 2f));
            if (!box.Intersects(bounds))
                continue;

            _stealthBoxes.Add(box);
            _stealthUids.Add(uid);
        }

        // Haze writes opaque alpha over a screen-aligned quad, which rotates in world space.
        _hazeQuads.Clear();
        var drives = _entManager.EntityQueryEnumerator<WarpDriveComponent, SpriteComponent, TransformComponent>();
        while (drives.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (!sprite.Visible || xform.MapID != args.MapId ||
                !sprites.HasPostShader((uid, sprite), WarpDriveHazeSystem.PostShaderId))
                continue;

            var silhouette = MakeSilhouette(uid, sprite, xform, worldToLocal, xforms);
            if (!PostShaderRegion(silhouette, view, out var center, out var quadSize))
                continue;

            var quad = UIBox2.FromDimensions(center - quadSize / 2, quadSize);
            var box = LocalBoxToWorld(quad, localToWorld).Enlarged(ExcludeMargin);
            if (!box.Intersects(bounds))
                continue;

            _stealthBoxes.Add(box);
            _hazeQuads.Add(quad);
        }

        if (_stealthBoxes.Count == 0)
        {
            _stealthUids.Clear();
            return blank;
        }

        // Every caster sprite that overlaps a stealth entity or a haze quad, from the same tree the renderer
        // draws from.
        _otherFlat.Clear();
        _otherUpright.Clear();
        _queriedSprites.Clear();
        var tree = _entManager.System<SpriteTreeSystem>();
        foreach (var box in _stealthBoxes)
        {
            _treeResults.Clear();
            tree.QueryAabb(_treeResults, args.MapId, box);
            foreach (var (uid, sprite, xform) in _treeResults)
            {
                // Deduplicate before transforms and list insertion, not just before drawing.
                if (!_queriedSprites.Add(uid))
                    continue;

                if (_stealthUids.Contains(uid))
                {
                    _stealthed.Add(MakeSilhouette(uid, sprite, xform, worldToLocal, xforms));
                    continue;
                }

                // Stealthed but not on screen, or a second stealth entity: never a caster.
                if (!sprite.Visible || _entManager.TryGetComponent(uid, out StealthComponent? stealth) && stealth.Enabled)
                    continue;

                // Only what is drawn between the floor capture and this pass casts.
                var depth = sprite.DrawDepth;
                if (depth < (int) DrawDepth.DeadMobs || depth >= ZIndex)
                    continue;

                var upright = depth >= (int) DrawDepth.Mobs && depth < (int) DrawDepth.Doors;
                (upright ? _otherUpright : _otherFlat).Add(MakeSilhouette(uid, sprite, xform, worldToLocal, xforms));
            }
        }

        _stealthUids.Clear();
        _queriedSprites.Clear();

        var handle = args.WorldHandle;
        var exclude = DrawSilhouettes(handle, screen, res, ref res.Exclude, "pirate-ao-exclude",
            _stealthed, ExcludeOffsets, false, view, _hazeQuads);
        var otherFlat = DrawSilhouettes(handle, screen, res, ref res.OtherFlat, "pirate-ao-other-flat",
            _otherFlat, NoOffsets, true, view);
        var otherUpright = DrawSilhouettes(handle, screen, res, ref res.OtherUpright, "pirate-ao-other-upright",
            _otherUpright, NoOffsets, true, view);
        _hazeQuads.Clear();

        return new StealthTextures(exclude, otherFlat, otherUpright);
    }

    private static Box2 LocalBoxToWorld(UIBox2 box, Matrix3x2 localToWorld)
    {
        var a = Vector2.Transform(new Vector2(box.Left, box.Top), localToWorld);
        var b = Vector2.Transform(new Vector2(box.Right, box.Top), localToWorld);
        var c = Vector2.Transform(new Vector2(box.Right, box.Bottom), localToWorld);
        var d = Vector2.Transform(new Vector2(box.Left, box.Bottom), localToWorld);
        return new Box2(
            MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X)),
            MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y)),
            MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X)),
            MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y)));
    }

    private bool PostShaderRegion(in Silhouette silhouette, in SilhouetteView view, out Vector2i center, out Vector2i size)
    {
        var bounds = view.WorldToLocal.TransformBox(_entManager.System<SpriteSystem>().CalculateBounds(
                (silhouette.Uid, silhouette.Sprite), silhouette.WorldPosition, silhouette.Rotation, view.EyeRotation))
            .Translated(silhouette.Snap);
        size = PostShaderTargetSize(bounds);
        center = (Vector2i) bounds.Center;
        return size.X > 0 && size.Y > 0;
    }

    private Silhouette MakeSilhouette(
        EntityUid uid,
        SpriteComponent sprite,
        TransformComponent xform,
        Matrix3x2 worldToLocal,
        SharedTransformSystem xforms)
    {
        var (position, rotation) = xforms.GetWorldPositionRotation(xform);
        var pixel = Vector2.Transform(position, worldToLocal);
        var snap = Vector2.Zero;

        if (xform.GridUid is { } grid)
        {
            if (!_gridSnaps.TryGetValue(grid, out snap))
            {
                var gridPixel = Vector2.Transform(xforms.GetWorldPosition(grid), worldToLocal);
                snap = gridPixel.Rounded() - gridPixel;
                _gridSnaps[grid] = snap;
            }

            pixel += snap;
        }

        return new Silhouette(uid, sprite, pixel, rotation, position, snap);
    }

    private Texture DrawSilhouettes(
        DrawingHandleWorld handle,
        DrawingHandleScreen screen,
        SpriteAmbientOcclusionResources res,
        ref IRenderTexture? target,
        string targetName,
        List<Silhouette> silhouettes,
        Vector2[] offsets,
        bool postShaders,
        in SilhouetteView view,
        List<UIBox2>? quads = null)
    {
        // Skip allocation and clear work when the group has no coverage.
        if (silhouettes.Count == 0 && (quads == null || quads.Count == 0))
            return Texture.Transparent;

        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref target, view.TargetSize, targetName);
        var viewCopy = view;
        handle.RenderInRenderTarget(target!, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            foreach (var silhouette in silhouettes)
            {
                if (postShaders && TryGetPostShaders(silhouette.Sprite))
                {
                    DrawWithPostShaders(handle, screen, res, silhouette, viewCopy);
                    continue;
                }

                foreach (var offset in offsets)
                {
                    DrawRaw(screen, silhouette, silhouette.Pixel + offset, viewCopy);
                }
            }

            if (quads != null)
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(null);
                foreach (var quad in quads)
                {
                    screen.DrawRect(quad, Color.White);
                }
            }

            handle.UseShader(null);
        }, Color.Transparent);
        return target!.Texture;
    }

    private static void DrawRaw(DrawingHandleScreen screen, in Silhouette silhouette, Vector2 pixel, in SilhouetteView view)
    {
        var sprite = silhouette.Sprite;
        Direction? direction = sprite.EnableDirectionOverride ? sprite.DirectionOverride : null;
        // DrawEntity reverses the eye rotation, so apply it to the world rotation.
        screen.DrawEntity(silhouette.Uid, pixel, view.DrawScale, silhouette.Rotation + view.EyeRotation, Angle.Zero,
            direction, sprite);
    }

    private bool TryGetPostShaders(SpriteComponent sprite)
    {
        _postShaders.Clear();
        foreach (var entry in _entManager.System<SpriteSystem>().GetPostShaders(sprite))
        {
            if (entry.GetScreenTexture)
            {
                _postShaders.Clear();
                return false;
            }

            _postShaders.Add((entry.Shader, ScreenUvPostShaders.Contains(entry.Id)));
        }

        return _postShaders.Count > 0;
    }

    // Post-shader alpha must use UV2 or pixel offsets: the engine pool can return a larger texture.
    private void DrawWithPostShaders(
        DrawingHandleWorld handle,
        DrawingHandleScreen screen,
        SpriteAmbientOcclusionResources res,
        Silhouette silhouette,
        SilhouetteView view)
    {
        if (!PostShaderRegion(silhouette, view, out var center, out var size))
        {
            DrawRaw(screen, silhouette, silhouette.Pixel, view);
            return;
        }

        var textureSize = PostShaderPoolBucket(size);
        var (source, spare) = res.GetPostShaderTargets(_clyde, textureSize);
        var local = silhouette with { Pixel = silhouette.Pixel - center + textureSize / 2 };

        handle.RenderInRenderTarget(source, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            DrawRaw(screen, local, local.Pixel, view);
        }, Color.Transparent);

        // Clyde.GetEntityPostRenderTargetSubRegion / GetEntityPostRenderTargetBox: the centred region.
        var offset = (textureSize - size) / 2;
        var region = UIBox2.FromDimensions(offset, size);
        var finalBox = UIBox2.FromDimensions(center - size / 2, size);
        for (var i = 0; i < _postShaders.Count - 1; i++)
        {
            // An intermediate pass that reads SCREEN_UV cannot be matched here (see DrawScreenUvPass): it keeps
            // the region quad. Today the only such shader (slasher_boogeyman) always runs last.
            var from = source;
            var shader = _postShaders[i].Shader;
            handle.RenderInRenderTarget(spare, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(shader);
                screen.DrawTextureRectRegion(from.Texture, region, region);
                screen.UseShader(null);
            }, Color.Transparent);

            (source, spare) = (spare, source);
        }

        // Back in the coverage target, at the renderer's final box: the alpha adds up like any other sprite.
        var (last, lastScreenUv) = _postShaders[^1];
        screen.SetTransform(Matrix3x2.Identity);
        screen.UseShader(last);
        if (lastScreenUv)
            DrawScreenUvPass(screen, source.Texture, finalBox, region, view.TargetSize);
        else
            screen.DrawTextureRectRegion(source.Texture, finalBox, region);
        screen.UseShader(null);
    }

    // Content cannot set SCREEN_UV_RECT; a viewport-sized quad supplies screen-relative UVs.
    // Shaders that read both SCREEN_UV and UV2 cannot be matched here.
    private static void DrawScreenUvPass(
        DrawingHandleScreen screen,
        Texture texture,
        UIBox2 finalBox,
        UIBox2 region,
        Vector2i targetSize)
    {
        // Target pixel p samples texel p - finalBox.TopLeft + region.TopLeft, as in the region-sized quad.
        var shift = region.TopLeft - finalBox.TopLeft;
        var full = UIBox2.FromDimensions(Vector2.Zero, targetSize);
        screen.DrawTextureRectRegion(texture, full, UIBox2.FromDimensions(shift, targetSize));
    }

    /// <summary>
    /// Runs the engine blur on an R8 (linear) mask with that mask as the bound target. Without hardware sRGB
    /// (compatibility mode: GLES on Windows), the engine emulates sRGB in its shaders. BlurRenderTarget sets the
    /// emulation flags once, before it binds its own targets: "read" from the source texture (R8, not sRGB) and
    /// "write" from the CURRENTLY bound target. Called from the overlay, that is the viewport, which is flagged
    /// sRGB, so each of the six passes would encode its output to sRGB again and turn the faint edge of every
    /// shadow into a wide dark halo. With the R8 mask bound, the write flag is off too. No clear: the mask is the
    /// blur's input.
    /// </summary>
    private void BlurLinearMask(DrawingHandleWorld handle, IClydeViewport viewport, IEye eye, IRenderTexture mask,
        IRenderTexture buffer)
    {
        handle.RenderInRenderTarget(mask, () => _clyde.BlurRenderTarget(viewport, mask, buffer, eye, ShadowBlur), null);
    }

    // Matches the private renderer bucket calculation.
    private static Vector2i PostShaderPoolBucket(Vector2i size)
    {
        static int Bucket(int value)
        {
            var v = Math.Max(value, 32);
            var result = 1;
            while (result < v)
            {
                result <<= 1;
            }

            return result;
        }

        return new Vector2i(Bucket(size.X), Bucket(size.Y));
    }

    // Matches the private renderer target size calculation.
    private static Vector2i PostShaderTargetSize(Box2 screenBounds)
    {
        var size = (Vector2i) (screenBounds.Size * PostShadeScale).Rounded();
        if (size.X % 2 != 0)
            size.X++;
        if (size.Y % 2 != 0)
            size.Y++;

        return size;
    }

    private Texture DrawForceFlat(in OverlayDrawArgs args, SpriteAmbientOcclusionResources res, IEye eye, Vector2i size)
    {
        _flatBoxes.Clear();
        _flatEntities.Clear();
        var lookup = _entManager.System<EntityLookupSystem>();
        var xforms = _entManager.System<SharedTransformSystem>();
        var bounds = args.WorldAABB;

        // Spatial query: the client keeps every entity it has ever seen, so a component scan would grow.
        lookup.GetEntitiesIntersecting(args.MapId, bounds, _flatEntities, LookupFlags.Static | LookupFlags.Approximate);

        foreach (var ent in _flatEntities)
        {
            if (!_entManager.TryGetComponent(ent.Owner, out FixturesComponent? fixtures) || fixtures.FixtureCount == 0)
                continue;

            var (worldPos, worldRot) = xforms.GetWorldPositionRotation(ent.Owner);
            var transform = new Transform(worldPos, worldRot);
            Box2? box = null;
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    var aabb = fixture.Shape.ComputeAABB(transform, i);
                    box = box?.Union(aabb) ?? aabb;
                }
            }

            if (box is { } found)
                _flatBoxes.Add(found.Enlarged(ForceFlatMargin));
        }

        return DrawBoxes(args, ref res.ForceFlat, "pirate-ao-force-flat", _flatBoxes, eye, size);
    }

    private Texture DrawBoxes(in OverlayDrawArgs args, ref IRenderTexture? target, string name, List<Box2> boxes, IEye eye, Vector2i size)
    {
        if (boxes.Count == 0)
            return Texture.Transparent;

        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref target, size, name);
        var rt = target!;

        var handle = args.WorldHandle;
        var scale = TargetScale(args.Viewport, size);

        handle.RenderInRenderTarget(rt, () =>
        {
            handle.SetTransform(rt.GetWorldToLocalMatrix(eye, scale));
            foreach (var box in boxes)
            {
                handle.DrawRect(box, Color.White);
            }
        }, Color.Transparent);

        return rt.Texture;
    }

    private static Vector2 TargetScale(IClydeViewport viewport, Vector2i size)
    {
        var lightScale = size / (Vector2) viewport.Size;
        return viewport.RenderScale / (Vector2.One / lightScale);
    }

    protected override void DisposeBehavior()
    {
        _ui.OnPostDrawUIRoot -= OnPostDrawUIRoot;
        _mask.Dispose();
        _simpleMask.Dispose();
        _downsample.Dispose();
        _composite.Dispose();
        base.DisposeBehavior();
    }
}
