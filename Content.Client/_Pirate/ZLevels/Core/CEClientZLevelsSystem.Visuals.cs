/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Damage.Systems;
using Content.Client.Movement.Systems;
using Content.Shared._DV.Carrying;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>Updates client-side Z-elevation visuals.</summary>
public sealed partial class CEClientZLevelsSystem
{
    private const float RenderSnapDistance = 0.34f;
    private const float ElevatedEnterHeight = 0.04f;
    private const float ElevatedExitHeight = 0.01f;
    private const float RenderSettleEpsilon = 0.0005f;
    private const float SpriteOffsetEpsilon = 0.0001f;

    private float _renderSmoothingTau = 0.05f;
    private float _renderMaxLag = 0.12f;

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<BeingCarriedComponent> _carriedQuery;
    private EntityQuery<BuckleComponent> _buckleQuery;

    // Continue driving bodies with pending visual offsets after deactivation.
    private readonly HashSet<EntityUid> _inactiveVisuals = new();
    private readonly List<EntityUid> _inactiveVisualScratch = new();

    private void InitializeVisuals()
    {
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _carriedQuery = GetEntityQuery<BeingCarriedComponent>();
        _buckleQuery = GetEntityQuery<BuckleComponent>();

        _cfg.OnValueChanged(CCVars.CEZLevelsRenderSmoothing,
            value => _renderSmoothingTau = MathF.Max(0f, value),
            invokeImmediately: true);
        _cfg.OnValueChanged(CCVars.CEZLevelsRenderMaxLag,
            value => _renderMaxLag = MathF.Max(RenderSettleEpsilon, value),
            invokeImmediately: true);

        // Run before ContentEyeSystem to keep camera and sprite heights in sync.
        UpdatesBefore.Add(typeof(ContentEyeSystem));

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnBodyStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, ComponentRemove>(OnBodyRemove);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZPhysicsActivationChangedEvent>(OnActivationChanged);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentStartup>(OnItemZPhysicsStartup);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentRemove>(OnItemZPhysicsRemove);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var uid in ActiveBodies)
        {
            _inactiveVisuals.Remove(uid);
            DriveBodyVisuals(uid, frameTime);
        }

        DriveInactiveVisuals(frameTime);
        DriveCarriedVisuals(frameTime);
        DriveBuckledVisuals(frameTime);
        DriveItemVisuals();
        TrackStaminaAnimationOffsets();
    }

    private void DriveInactiveVisuals(float frameTime)
    {
        if (_inactiveVisuals.Count == 0)
            return;

        _inactiveVisualScratch.Clear();
        _inactiveVisualScratch.AddRange(_inactiveVisuals);

        foreach (var uid in _inactiveVisualScratch)
        {
            if (IsBodyActive(uid))
            {
                _inactiveVisuals.Remove(uid);
                continue;
            }

            if (!DriveBodyVisuals(uid, frameTime))
                _inactiveVisuals.Remove(uid);
        }
    }

    private bool DriveBodyVisuals(EntityUid uid, float frameTime)
    {
        if (!ZPhysQuery.TryComp(uid, out var zPhys) ||
            !_spriteQuery.TryComp(uid, out var sprite) ||
            !TransformQuery.TryComp(uid, out var xform))
        {
            return false;
        }

        // Carried mobs use their carrier's height to avoid competing updates.
        if (_carriedQuery.HasComp(uid))
            return false;

        // Buckled riders are updated from their vehicle in the dedicated pass.
        if (_buckleQuery.TryComp(uid, out var buckle) &&
            buckle.BuckledTo is { } strap &&
            ZPhysQuery.HasComp(strap))
        {
            return false;
        }

        EnsureBodyVisualDefaults(zPhys, sprite);

        var target = GetVisualsLocalPosition((uid, zPhys), xform);
        var height = AdvanceRenderHeight(zPhys, target, frameTime);
        var elevated = UpdateElevatedLatch(zPhys, zPhys, height);

        return ApplyElevationVisuals(
            (uid, sprite),
            zPhys,
            height,
            elevated,
            zPhys.SpriteOffsetDefault,
            zPhys.DrawDepthDefault,
            zPhys.NoRotDefault,
            zPhys.PreserveDynamicDrawDepth);
    }

    private void DriveCarriedVisuals(float frameTime)
    {
        var query = EntityQueryEnumerator<BeingCarriedComponent, CEZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var carried, out var zPhys, out var sprite))
        {
            if (!ZPhysQuery.TryComp(carried.Carrier, out var carrierZ) ||
                !TransformQuery.TryComp(carried.Carrier, out var carrierXform))
            {
                continue;
            }

            EnsureBodyVisualDefaults(zPhys, sprite);

            // Use raw height to avoid smoothing twice.
            var target = GetVisualsLocalPosition((carried.Carrier, carrierZ), carrierXform);
            var height = AdvanceRenderHeight(zPhys, target, frameTime);
            var elevated = UpdateElevatedLatch(zPhys, carrierZ, height);

            ApplyElevationVisuals(
                (uid, sprite),
                zPhys,
                height,
                elevated,
                zPhys.SpriteOffsetDefault,
                zPhys.DrawDepthDefault,
                zPhys.NoRotDefault);
        }
    }

    /// <summary>Updates mounted riders from their Z-physics strap.</summary>
    private void DriveBuckledVisuals(float frameTime)
    {
        var query = EntityQueryEnumerator<BuckleComponent, CEZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var buckle, out var riderZ, out var sprite))
        {
            if (buckle.BuckledTo is not { } strap ||
                !ZPhysQuery.TryComp(strap, out var strapZ) ||
                !TransformQuery.TryComp(strap, out var strapXform))
            {
                continue;
            }

            EnsureBodyVisualDefaults(riderZ, sprite);

            var target = GetVisualsLocalPosition((strap, strapZ), strapXform);
            var height = AdvanceRenderHeight(riderZ, target, frameTime);
            var elevated = UpdateElevatedLatch(riderZ, strapZ, height);

            ApplyElevationVisuals(
                (uid, sprite),
                riderZ,
                height,
                elevated,
                riderZ.SpriteOffsetDefault,
                riderZ.DrawDepthDefault,
                riderZ.NoRotDefault,
                preserveDynamicDrawDepth: true);
        }
    }

    private void DriveItemVisuals()
    {
        var query = EntityQueryEnumerator<CEZItemPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zItem, out var sprite))
        {
            var height = MathF.Max(zItem.LocalPosition, 0f);

            // Do not compete with throw animations until the item has fallen.
            if (height <= 0f)
            {
                if (zItem.VisualsApplied)
                {
                    RestoreItemVisuals((uid, zItem), sprite);
                    zItem.VisualsApplied = false;
                }

                continue;
            }

            EnsureItemVisualDefaults((uid, zItem), sprite);
            sprite.NoRotation = true;
            _sprite.SetOffset((uid, sprite), zItem.SpriteOffsetDefault + new Vector2(0f, height * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), (int) Shared.DrawDepth.DrawDepth.OverMobs);
            zItem.VisualsApplied = true;
        }
    }

    private void TrackStaminaAnimationOffsets()
    {
        var query = EntityQueryEnumerator<StaminaComponent, SpriteComponent, CEZPhysicsComponent>();
        while (query.MoveNext(out var uid, out var stamina, out var sprite, out _))
        {
            if (_animation.HasRunningAnimation(uid, StaminaSystem.StaminaAnimationKey))
                stamina.StartOffset = sprite.Offset;
        }
    }

    /// <summary>Smooths render height, snapping on first sample, Z changes, and large corrections.</summary>
    private float AdvanceRenderHeight(CEZPhysicsComponent comp, float target, float frameTime)
    {
        var snap = !comp.RenderHeightInitialized ||
                   comp.RenderZLevel != comp.CurrentZLevel ||
                   _renderSmoothingTau <= 0f ||
                   frameTime <= 0f ||
                   MathF.Abs(target - comp.RenderHeight) >= RenderSnapDistance;

        comp.RenderHeightInitialized = true;
        comp.RenderZLevel = comp.CurrentZLevel;

        if (snap)
        {
            comp.RenderHeight = target;
            return target;
        }

        var height = InterpolateRenderHeight(
            comp.RenderHeight,
            target,
            frameTime,
            _renderSmoothingTau,
            _renderMaxLag);

        comp.RenderHeight = height;
        return height;
    }

    internal static float InterpolateRenderHeight(
        float current,
        float target,
        float frameTime,
        float smoothingTau,
        float maxLag)
    {
        var height = MathHelper.Lerp(current, target, 1f - MathF.Exp(-frameTime / smoothingTau));
        height = Math.Clamp(height, target - maxLag, target + maxLag);

        return MathF.Abs(target - height) < RenderSettleEpsilon ? target : height;
    }

    /// <summary>Uses hysteresis to avoid flickering draw order around ground level.</summary>
    private static bool UpdateElevatedLatch(
        CEZPhysicsComponent visualComp,
        CEZPhysicsComponent supportComp,
        float height)
    {
        var elevated = supportComp.CurrentStickyGround ||
                       supportComp.CurrentGroundHeight > ElevatedEnterHeight ||
                       (visualComp.RenderElevated ? height > ElevatedExitHeight : height > ElevatedEnterHeight);

        visualComp.RenderElevated = elevated;
        return elevated;
    }

    private bool ApplyElevationVisuals(
        Entity<SpriteComponent> ent,
        CEZPhysicsComponent visualComp,
        float height,
        bool elevated,
        Vector2 offsetDefault,
        int depthDefault,
        bool noRotDefault,
        bool preserveDynamicDrawDepth = false)
    {
        var offset = offsetDefault + new Vector2(0f, height * ZLevelOffset);
        if (Vector2.DistanceSquared(ent.Comp.Offset, offset) > SpriteOffsetEpsilon * SpriteOffsetEpsilon)
            _sprite.SetOffset((ent.Owner, ent.Comp), offset);

        if (elevated)
        {
            if (preserveDynamicDrawDepth && visualComp.DrawDepthBeforeElevation == null)
                visualComp.DrawDepthBeforeElevation = ent.Comp.DrawDepth;

            var elevatedDepth = (int) Shared.DrawDepth.DrawDepth.OverMobs;
            if (ent.Comp.DrawDepth != elevatedDepth)
                _sprite.SetDrawDepth((ent.Owner, ent.Comp), elevatedDepth);
        }
        else if (preserveDynamicDrawDepth)
        {
            if (visualComp.DrawDepthBeforeElevation is { } previousDepth)
            {
                if (ent.Comp.DrawDepth != previousDepth)
                    _sprite.SetDrawDepth((ent.Owner, ent.Comp), previousDepth);

                visualComp.DrawDepthBeforeElevation = null;
            }
        }
        else
        {
            if (ent.Comp.DrawDepth != depthDefault)
                _sprite.SetDrawDepth((ent.Owner, ent.Comp), depthDefault);

            visualComp.DrawDepthBeforeElevation = null;
        }

        var noRotation = elevated || noRotDefault;
        if (ent.Comp.NoRotation != noRotation)
            ent.Comp.NoRotation = noRotation;

        return elevated || MathF.Abs(height) > SpriteOffsetEpsilon;
    }

    private void OnBodyStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (_spriteQuery.TryComp(ent, out var sprite))
            EnsureBodyVisualDefaults(ent.Comp, sprite);
    }

    private void OnBodyRemove(Entity<CEZPhysicsComponent> ent, ref ComponentRemove args)
    {
        _inactiveVisuals.Remove(ent.Owner);

        if (!ent.Comp.VisualsInitialized || !_spriteQuery.TryComp(ent, out var sprite))
            return;

        RestoreBodyVisuals(ent.Comp, (ent.Owner, sprite));
    }

    private void OnActivationChanged(Entity<CEZPhysicsComponent> ent, ref CEZPhysicsActivationChangedEvent args)
    {
        if (args.Active)
        {
            _inactiveVisuals.Remove(ent.Owner);
            return;
        }

        if (ent.Comp.RenderElevated ||
            MathF.Abs(ent.Comp.RenderHeight) > SpriteOffsetEpsilon ||
            MathF.Abs(ent.Comp.LocalPosition) > SpriteOffsetEpsilon)
        {
            _inactiveVisuals.Add(ent.Owner);
        }
        else if (ent.Comp.VisualsInitialized && _spriteQuery.TryComp(ent, out var sprite))
        {
            RestoreBodyVisuals(ent.Comp, (ent.Owner, sprite));
        }
    }

    /// <summary>Captures defaults lazily because CEZPhysics can initialize before Sprite.</summary>
    private static void EnsureBodyVisualDefaults(CEZPhysicsComponent comp, SpriteComponent sprite)
    {
        if (comp.VisualsInitialized)
            return;

        comp.NoRotDefault = sprite.NoRotation;
        comp.DrawDepthDefault = sprite.DrawDepth;
        comp.SpriteOffsetDefault = sprite.Offset;
        comp.VisualsInitialized = true;
    }

    private void RestoreBodyVisuals(CEZPhysicsComponent comp, Entity<SpriteComponent> sprite)
    {
        sprite.Comp.NoRotation = comp.NoRotDefault;
        _sprite.SetOffset((sprite.Owner, sprite.Comp), comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((sprite.Owner, sprite.Comp), comp.DrawDepthBeforeElevation ?? comp.DrawDepthDefault);
        comp.DrawDepthBeforeElevation = null;
        comp.RenderElevated = false;
        comp.RenderHeight = 0f;
        comp.RenderHeightInitialized = false;
    }

    private void OnItemZPhysicsStartup(Entity<CEZItemPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (_spriteQuery.TryComp(ent, out var sprite))
            EnsureItemVisualDefaults(ent, sprite);
    }

    private void OnItemZPhysicsRemove(Entity<CEZItemPhysicsComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.VisualsInitialized && _spriteQuery.TryComp(ent, out var sprite))
            RestoreItemVisuals(ent, sprite);
    }

    private void RestoreItemVisuals(Entity<CEZItemPhysicsComponent> ent, SpriteComponent sprite)
    {
        sprite.NoRotation = ent.Comp.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), ent.Comp.DrawDepthDefault);
    }

    private static void EnsureItemVisualDefaults(Entity<CEZItemPhysicsComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.VisualsInitialized)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
        ent.Comp.VisualsInitialized = true;
    }

    public float GetRenderHeight(Entity<CEZPhysicsComponent?> ent, TransformComponent? xform = null)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !Resolve(ent, ref xform, false))
            return 0f;

        if (xform.ParentUid != xform.MapUid && ZPhysQuery.TryComp(xform.ParentUid, out var parentZPhys))
            return parentZPhys.RenderHeightInitialized ? parentZPhys.RenderHeight : parentZPhys.LocalPosition;

        return ent.Comp.RenderHeightInitialized ? ent.Comp.RenderHeight : ent.Comp.LocalPosition;
    }

    /// <summary>Returns the screen-facing Z offset for independently rendered overlays.</summary>
    public Vector2 GetRenderScreenOffset(EntityUid ent)
    {
        if (!ZPhysQuery.TryComp(ent, out var zPhys) || !TransformQuery.TryComp(ent, out var xform))
            return Vector2.Zero;

        return new Vector2(0f, GetRenderHeight((ent, zPhys), xform) * ZLevelOffset);
    }
}
