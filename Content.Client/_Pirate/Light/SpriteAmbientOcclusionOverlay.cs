// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Pirate.Light;
using Content.Shared.Stealth.Components;
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
    public IRenderTexture? MaskBlur;
    public IRenderTexture? Exclude;
    public IRenderTexture? ForceFlat;

    // Prevents reuse of stale captures.
    public int Captured;

    public void Dispose()
    {
        foreach (var capture in Captures)
        {
            capture?.Dispose();
        }

        Mask?.Dispose();
        MaskBlur?.Dispose();
        Exclude?.Dispose();
        ForceFlat?.Dispose();
    }

    public static void EnsureTarget(IClyde clyde, ref IRenderTexture? target, Vector2i size, string name)
    {
        if (target?.Texture.Size == size)
            return;

        target?.Dispose();
        target = clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
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

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly OverlayResourceCache<SpriteAmbientOcclusionResources> _resources;
    private readonly ShaderInstance _copy;
    private readonly int _slot;
    private readonly string _targetName;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    protected SpriteAmbientOcclusionCaptureOverlay(
        OverlayResourceCache<SpriteAmbientOcclusionResources> resources,
        SpriteAmbientOcclusionCapture capture)
    {
        IoCManager.InjectDependencies(this);
        _resources = resources;
        _copy = _proto.Index(CopyShader).InstanceUnique();
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
        if (ScreenTexture == null)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new SpriteAmbientOcclusionResources());
        var size = args.Viewport.RenderTarget.Size;
        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.Captures[_slot], size, _targetName);
        var target = res.Captures[_slot]!;

        var handle = args.WorldHandle;
        _copy.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.RenderInRenderTarget(target, () =>
        {
            handle.UseShader(_copy);
            SpriteAmbientOcclusionResources.DrawFullTarget(handle, size);
        }, Color.Transparent);

        res.Captured |= 1 << _slot;
        AfterCapture(args);
    }

    protected virtual void AfterCapture(in OverlayDrawArgs args)
    {
    }

    protected override void DisposeBehavior()
    {
        _copy.Dispose();
        base.DisposeBehavior();
    }
}

public sealed class SpriteAmbientOcclusionFloorCaptureOverlay : SpriteAmbientOcclusionCaptureOverlay
{
    private static readonly ProtoId<ShaderPrototype> ClearAlphaShader = "PirateAmbientOcclusionClearAlpha";

    private readonly ShaderInstance _clearAlpha;

    public SpriteAmbientOcclusionFloorCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
        : base(resources, SpriteAmbientOcclusionCapture.Floor)
    {
        _clearAlpha = IoCManager.Resolve<IPrototypeManager>().Index(ClearAlphaShader).InstanceUnique();
    }

    protected override void AfterCapture(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _clearAlpha.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_clearAlpha);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _clearAlpha.Dispose();
        base.DisposeBehavior();
    }
}

public sealed class SpriteAmbientOcclusionBeforeMobsCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    : SpriteAmbientOcclusionCaptureOverlay(resources, SpriteAmbientOcclusionCapture.BeforeMobs);

public sealed class SpriteAmbientOcclusionAfterMobsCaptureOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    : SpriteAmbientOcclusionCaptureOverlay(resources, SpriteAmbientOcclusionCapture.AfterMobs);

public sealed class SpriteAmbientOcclusionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> MaskShader = "PirateAmbientOcclusionMask";
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

    // The engine blur multiplier gives a Gaussian sigma of about 2 pixels.
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
    private readonly ShaderInstance _composite;
    // Fixture bounds are exact; the sprite strip can be a pixel wider.
    private const float ForceFlatMargin = 1f / EyeManager.PixelsPerMeter;

    private readonly List<(EntityUid Uid, SpriteComponent Sprite, Vector2 Position, Angle Rotation)> _stealthed = new();
    private DrawingHandleScreen? _screenHandle;
    private readonly List<Box2> _flatBoxes = new();
    private readonly HashSet<Entity<AmbientOcclusionFlatComponent>> _flatEntities = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    public SpriteAmbientOcclusionOverlay(OverlayResourceCache<SpriteAmbientOcclusionResources> resources)
    {
        IoCManager.InjectDependencies(this);
        _resources = resources;
        _mask = _proto.Index(MaskShader).InstanceUnique();
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

        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.Mask, size, "pirate-ao-mask");
        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.MaskBlur, size, "pirate-ao-mask-blur");

        var handle = args.WorldHandle;
        var exclude = DrawStealthExclusion(args, res, eye, size);
        var forceFlat = DrawForceFlat(args, res, eye, size);

        // Render pixels per world pixel, so the offsets stay in world pixels at any zoom or render scale.
        var renderPerWorldPixel = args.Viewport.RenderScale.Y * eye.Scale.Y;
        var pixelSize = Vector2.One / size;

        _mask.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _mask.SetParameter("BEFORE_MOBS_TEXTURE", beforeMobs.Texture);
        _mask.SetParameter("AFTER_MOBS_TEXTURE", afterMobs.Texture);
        _mask.SetParameter("EXCLUDE_TEXTURE", exclude);
        _mask.SetParameter("FLAT_TEXTURE", forceFlat);
        _mask.SetParameter("STEP", new Vector2(0f, renderPerWorldPixel * pixelSize.Y));
        _mask.SetParameter("FADE", UprightFade);
        handle.RenderInRenderTarget(res.Mask!, () =>
        {
            handle.UseShader(_mask);
            SpriteAmbientOcclusionResources.DrawFullTarget(handle, size);
        }, Color.Transparent);

        _clyde.BlurRenderTarget(args.Viewport, res.Mask!, res.MaskBlur!, eye, ShadowBlur);

        _composite.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _composite.SetParameter("FLOOR_TEXTURE", floor.Texture);
        _composite.SetParameter("MASK_TEXTURE", res.Mask!.Texture);
        _composite.SetParameter("EXCLUDE_TEXTURE", exclude);
        _composite.SetParameter("SHIFT", new Vector2(0f, ShadowOffset * renderPerWorldPixel));
        _composite.SetParameter("SHADOW", new Vector4(ShadowColor.R, ShadowColor.G, ShadowColor.B, ShadowColor.A));

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_composite);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private Texture DrawStealthExclusion(in OverlayDrawArgs args, SpriteAmbientOcclusionResources res, IEye eye, Vector2i size)
    {
        if (_screenHandle is not { Disposed: false } screen)
            return Texture.Transparent;

        _stealthed.Clear();
        var sprites = _entManager.System<SpriteSystem>();
        var xforms = _entManager.System<SharedTransformSystem>();
        var bounds = args.WorldAABB;

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

            var (position, rotation) = xforms.GetWorldPositionRotation(xform);
            if (Box2.CenteredAround(position, new Vector2(radius * 2f)).Intersects(bounds))
                _stealthed.Add((uid, sprite, position, rotation));
        }

        if (_stealthed.Count == 0)
            return Texture.Transparent;

        SpriteAmbientOcclusionResources.EnsureTarget(_clyde, ref res.Exclude, size, "pirate-ao-exclude");
        var rt = res.Exclude!;
        var worldToLocal = rt.GetWorldToLocalMatrix(eye, TargetScale(args.Viewport, size));
        var origin = Vector2.Transform(Vector2.Zero, worldToLocal);
        var renderPerMeter = (Vector2.Transform(Vector2.UnitX, worldToLocal) - origin).Length();
        var drawScale = new Vector2(renderPerMeter / EyeManager.PixelsPerMeter);
        var stealthed = _stealthed;

        var handle = args.WorldHandle;
        handle.RenderInRenderTarget(rt, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            foreach (var (uid, sprite, position, rotation) in stealthed)
            {
                var pixel = Vector2.Transform(position, worldToLocal);
                Direction? direction = sprite.EnableDirectionOverride ? sprite.DirectionOverride : null;
                foreach (var offset in ExcludeOffsets)
                {
                    // DrawEntity reverses the eye rotation, so apply it to the world rotation.
                    screen.DrawEntity(uid, pixel + offset, drawScale, rotation + eye.Rotation, Angle.Zero, direction, sprite);
                }
            }

            handle.UseShader(null);
        }, Color.Transparent);

        return rt.Texture;
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
        _composite.Dispose();
        base.DisposeBehavior();
    }
}
