// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Item.ItemToggle.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Pirate.Shared.Item.ItemToggle;

/// <summary>
/// Casts a configurable ray when an item is toggled on.
/// </summary>
public sealed class ItemToggleCastRaySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemToggleCastRayComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<ItemToggleCastRayComponent, LightToggleEvent>(OnLightToggled);
    }

    private void OnItemToggled(Entity<ItemToggleCastRayComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            return;

        var direction = (_transform.GetWorldRotation(ent.Owner) + Angle.FromDegrees(ent.Comp.RayOffsetDegrees)).ToVec();
        var ray = new CollisionRay(
            _transform.GetWorldPosition(ent.Owner),
            direction,
            (int) CollisionGroup.Opaque);

        EntityUid? user = args.User;
        if (user == null &&
            _containers.TryGetOuterContainer(ent.Owner, Transform(ent.Owner), out var container))
        {
            user = container.Owner;
        }

        var results = _physics.IntersectRayWithPredicate(
            _transform.GetMapId(ent.Owner),
            ray,
            maxLength: ent.Comp.RayLength,
            predicate: uid => uid == ent.Owner || user != null && uid == user.Value,
            returnOnFirstHit: false);

        // Results are distance-sorted, so only the nearest opaque fixture can be affected.
        foreach (var result in results)
        {
            foreach (var (_, component) in ent.Comp.RaiseEventAt)
            {
                if (!HasComp(result.HitEntity, component.Component.GetType()))
                    continue;

                var ev = new ItemToggleRayHitEvent();
                RaiseLocalEvent(result.HitEntity, ref ev);
                break;
            }

            break;
        }
    }

    // Handheld lights raise LightToggleEvent instead of ItemToggledEvent.
    private void OnLightToggled(Entity<ItemToggleCastRayComponent> ent, ref LightToggleEvent args)
    {
        var itemToggle = new ItemToggledEvent(Predicted: true, Activated: args.IsOn, User: null);
        OnItemToggled(ent, ref itemToggle);
    }
}
