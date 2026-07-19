// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Client/_CMU14/ZLevels/Core/CMUClientZLevelsSystem.cs

using System.Numerics;
using Content.Client._Pirate.ZLevels.Core; // Pirate: multiz - CEClientZLevelsSystem.ZLevelOffset (render offset cvar)
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client._Pirate.ZLevels.Shooting;

/// <summary>
/// Client-side renderer for the projectile visual offset components. Applies the offset to the
/// projectile sprite so its muzzle flash appears at the shooter's barrel on the source Z layer
/// instead of at the actual (target-layer) spawn point. On component shutdown the original
/// sprite offset is restored.
/// </summary>
public sealed class CMUZLevelClientShootingSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentStartup>(OnSyncedStartup);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentShutdown>(OnSyncedShutdown);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentStartup>(OnPredictedStartup);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentShutdown>(OnPredictedShutdown);
    }

    private void OnSyncedStartup(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent.Owner))
            return;
        TryApplyProjectileVisualOffset(ent.Owner, ent.Comp.Offset, ent.Comp.Depth, ref ent.Comp.OriginalOffset, ref ent.Comp.AppliedOffset);
    }

    private void OnSyncedShutdown(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        if (HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent.Owner))
            return;
        RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    private void OnPredictedStartup(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        TryApplyProjectileVisualOffset(ent.Owner, ent.Comp.Offset, ent.Comp.Depth, ref ent.Comp.OriginalOffset, ref ent.Comp.AppliedOffset);
    }

    private void OnPredictedShutdown(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Re-apply each frame so projectile rotation/render-rotation updates keep the offset
        // correctly oriented. Predicted-only projectiles take precedence — skip the synced
        // entry to avoid double-applying.
        var syncedQuery = EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (syncedQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
            if (HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid))
                continue;

            ApplyProjectileVisualOffset(uid, visual.Offset, visual.Depth, ref visual.OriginalOffset, ref visual.AppliedOffset, sprite, xform);
        }

        var predictedQuery = EntityQueryEnumerator<CMUZLevelPredictedProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (predictedQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
            ApplyProjectileVisualOffset(uid, visual.Offset, visual.Depth, ref visual.OriginalOffset, ref visual.AppliedOffset, sprite, xform);
        }
    }

    private bool TryApplyProjectileVisualOffset(
        EntityUid uid,
        Vector2 barrelShift,
        int depth,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !TryComp(uid, out TransformComponent? xform))
            return false;

        ApplyProjectileVisualOffset(uid, barrelShift, depth, ref originalOffset, ref appliedOffset, sprite, xform);
        return true;
    }

    private void ApplyProjectileVisualOffset(
        EntityUid uid,
        Vector2 barrelShift,
        int depth,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset,
        SpriteComponent sprite,
        TransformComponent xform)
    {
        // Add the render-displacement built from the live eye (the Z render pass shifts its eye by
        // renderDir * ZLevelOffset * depth). Live eye is the point of the split: lanos's eye
        // can be rotated, so this isn't axis-aligned and a baked constant would land off-line.
        // Pirate: multiz - must use the SAME offset the render pass uses (CEClientZLevelsSystem.ZLevelOffset,
        // the zlevels.ce_render_offset cvar), not the 0.7 physics constant ZLevelVisualOffset. The render
        // offset was changed 0.7 => 0.3 (commit 05b426b395) without updating this compensation, leaving a
        // ~0.4-tile vertical residual: invisible on N/S shots (slides along travel), but drops E/W shots
        // below the aim line.
        Angle negEyeRotation = _eye.CurrentEye.Rotation * -1;
        var renderDir = negEyeRotation.ToWorldVec();
        var worldOffset = barrelShift + renderDir * CEClientZLevelsSystem.ZLevelOffset * depth;

        // No-rotation sprites stay screen-aligned; rotated sprites need the offset in their own
        // local frame so it doesn't flip with the projectile.
        Angle renderRotation;
        if (sprite.NoRotation)
            renderRotation = _eye.CurrentEye.Rotation * -1;
        else
            renderRotation = _transformSystem.GetWorldRotation(xform);

        var localVisualOffset = (-renderRotation).RotateVec(worldOffset);

        // Capture the pristine sprite offset once so we can undo our shift on shutdown.
        originalOffset ??= sprite.Offset - appliedOffset;
        if (appliedOffset == localVisualOffset)
            return;

        _sprite.SetOffset((uid, sprite), originalOffset.Value + localVisualOffset);
        appliedOffset = localVisualOffset;
    }

    private void RestoreProjectileVisualOffset(EntityUid uid, Vector2? originalOffset)
    {
        if (originalOffset is { } original && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetOffset((uid, sprite), original);
    }
}
