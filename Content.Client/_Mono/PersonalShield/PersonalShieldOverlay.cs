using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.PersonalShield;

public sealed partial class PersonalShieldOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = null!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "PersonalShieldSkin";

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly InventorySystem _inventory;
    private readonly ShaderInstance _baseShader;

    // One shader instance per shield entity: uniforms are read from the live instance when the
    // batch is flushed, so sharing a single instance would render every shield with the last
    // entity's parameters (e.g. one shield's color bleeding into another).
    private readonly Dictionary<EntityUid, ShaderInstance> _shaderInstances = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public PersonalShieldOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _inventory = _entManager.System<InventorySystem>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        _baseShader = protoMan.Index(ShaderId).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;

        // Cancel the eye rotation so the shield is always "upright".
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var counterRot = Matrix3Helpers.CreateRotation(-eyeRot);

        CleanupShaderInstances();

        var query = _entManager.EntityQueryEnumerator<PersonalShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (shield.Runtime.Form <= 0f && shield.Runtime.Shatter <= 0f)
                continue;

            if (!_inventory.TryGetContainingEntity(uid, out var wearer))
                continue;

            if (!_entManager.TryGetComponent(wearer, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            if (!_entManager.TryGetComponent(wearer, out TransformComponent? xform) || xform.MapID != args.MapId)
                continue;

            if (!TryGetHitboxSize(wearer.Value, sprite, out var extents))
                continue;

            var size = extents * shield.Scale;
            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldBounds.CalcBoundingBox().Intersects(Box2.CenteredAround(worldPos, size)))
                continue;

            var shader = GetShaderInstance(uid);

            shader.SetParameter("progress", GetProgress(shield));
            shader.SetParameter("skin_color", shield.Color);
            shader.SetParameter("brightness", shield.Brightness);
            shader.SetParameter("pixel_grid", shield.PixelGrid);
            shader.SetParameter("hex_density", shield.HexDensity);
            shader.SetParameter("form_origin", shield.FormOrigin);
            shader.SetParameter("fill_level", shield.FillLevel);
            shader.SetParameter("line_level", shield.LineLevel);
            shader.SetParameter("rim_level", shield.RimLevel);
            shader.SetParameter("core_fade", shield.CoreFade);
            shader.SetParameter("shard_scale", shield.ShardScale);
            shader.SetParameter("alpha_bands", shield.AlphaBands);
            shader.SetParameter("breath_depth", shield.BreathDepth);

            handle.UseShader(shader);

            handle.SetTransform(Matrix3x2.Multiply(counterRot, Matrix3Helpers.CreateTranslation(worldPos)));
            handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private ShaderInstance GetShaderInstance(EntityUid uid)
    {
        if (!_shaderInstances.TryGetValue(uid, out var shader))
        {
            shader = _baseShader.Duplicate();
            _shaderInstances[uid] = shader;
        }

        return shader;
    }

    private void CleanupShaderInstances()
    {
        if (_shaderInstances.Count == 0)
            return;

        List<EntityUid>? dead = null;
        foreach (var (uid, _) in _shaderInstances)
        {
            if (_entManager.Deleted(uid) || !_entManager.HasComponent<PersonalShieldComponent>(uid))
                (dead ??= new List<EntityUid>()).Add(uid);
        }

        if (dead == null)
            return;

        foreach (var uid in dead)
        {
            if (_shaderInstances.Remove(uid, out var shader))
                shader.Dispose();
        }
    }

    protected override void DisposeBehavior()
    {
        foreach (var shader in _shaderInstances.Values)
            shader.Dispose();

        _shaderInstances.Clear();
        _baseShader.Dispose();
    }

    private bool TryGetHitboxSize(EntityUid uid, SpriteComponent sprite, out Vector2 extents)
    {
        extents = Vector2.Zero;

        if (_entManager.TryGetComponent(uid, out FixturesComponent? fixtures) && fixtures.FixtureCount > 0)
        {
            var identity = new Transform(Vector2.Zero, 0f);
            Box2? union = null;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                var aabb = fixture.Shape.ComputeAABB(identity, 0);
                union = union?.Union(aabb) ?? aabb;
            }

            if (union is { } box && box.Width > 0f && box.Height > 0f)
            {
                extents = box.Size;
                return true;
            }
        }

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        extents = bounds.Size;
        return extents is { X: > 0f, Y: > 0f };
    }

    private static float GetProgress(PersonalShieldComponent shield)
    {
        return shield.Runtime.Shatter > 0f
            ? 1f + MathF.Min(shield.Runtime.Shatter, 1f)
            : shield.Runtime.Form;
    }
}
