// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Pirate.Weapons.Ranged;

/// <summary>
/// Event-driven composite ammo provider. It only inspects the configured containers of the gun
/// receiving an ammo, container, examine, or UI event.
/// </summary>
public abstract class SharedMultiMagazineGunSystem : EntitySystem
{
    private const string AmmoExamineColor = "yellow";

    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<MultiMagazineAmmoProviderComponent> ent, ref MapInitEvent args)
    {
        MagazineSlotChanged(ent);
    }

    private void OnExamined(Entity<MultiMagazineAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(ent.Owner, ref ammo);
        args.PushMarkup(Loc.GetString("gun-magazine-examine",
            ("color", AmmoExamineColor),
            ("count", ammo.Count)));
    }

    private void OnUseInHand(Entity<MultiMagazineAmmoProviderComponent> ent, ref UseInHandEvent args)
    {
        var magazines = GetMagazineEntities(ent);
        foreach (var nested in magazines.Values)
        {
            if (nested is not { } uid)
                return;

            RaiseLocalEvent(uid, args);
        }

        _gun.UpdateAmmoCount(ent.Owner);
        UpdateMagazineAppearance(ent, magazines);
    }

    private void OnGetVerbs(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var magazines = GetMagazineEntities(ent);
        foreach (var nested in magazines.Values)
        {
            if (nested is not { } uid)
                return;

            RaiseLocalEvent(uid, args);
        }

        UpdateMagazineAppearance(ent, magazines);
    }

    protected virtual void OnSlotChanged(EntityUid uid,
        MultiMagazineAmmoProviderComponent component,
        ContainerModifiedMessage args)
    {
        if (component.Slots.ContainsKey(args.Container.ID))
            MagazineSlotChanged((uid, component));
    }

    private void MagazineSlotChanged(Entity<MultiMagazineAmmoProviderComponent> ent)
    {
        _gun.UpdateAmmoCount(ent.Owner);

        var magazines = GetMagazineEntities(ent);

        if (TryComp<AppearanceComponent>(ent.Owner, out var appearance))
        {
            var hasLoadedMagazine = magazines.Values.Any(uid => uid is not null);
            _appearance.SetData(ent.Owner, AmmoVisuals.MagLoaded, hasLoadedMagazine, appearance);
        }

        UpdateMagazineAppearance(ent, magazines);
    }

    public Dictionary<string, EntityUid?> GetMagazineEntities(Entity<MultiMagazineAmmoProviderComponent> ent)
    {
        var result = new Dictionary<string, EntityUid?>(ent.Comp.Slots.Count);
        foreach (var slotId in ent.Comp.Slots.Keys)
        {
            if (!_containers.TryGetContainer(ent.Owner, slotId, out var container) ||
                container is not ContainerSlot slot)
            {
                result[slotId] = null;
                continue;
            }

            result[slotId] = slot.ContainedEntity;
        }

        return result;
    }

    private void OnGetAmmoCount(Entity<MultiMagazineAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        var first = true;
        foreach (var (slotId, nested) in GetMagazineEntities(ent))
        {
            if (nested is not { } uid)
            {
                args.Count = 0;
                args.Capacity = 0;
                return;
            }

            var nestedEvent = new GetAmmoCountEvent
            {
                FireCostMultiplier = ent.Comp.Slots[slotId] ?? 1f,
            };
            RaiseLocalEvent(uid, ref nestedEvent);

            if (first)
            {
                args.Count = nestedEvent.Count;
                args.Capacity = nestedEvent.Capacity;
                first = false;
                continue;
            }

            args.Count = Math.Min(args.Count, nestedEvent.Count);
            args.Capacity = Math.Min(args.Capacity, nestedEvent.Capacity);
        }

        if (first)
        {
            args.Count = 0;
            args.Capacity = 0;
        }
    }

    private void OnTakeAmmo(Entity<MultiMagazineAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        var count = new GetAmmoCountEvent();
        RaiseLocalEvent(ent.Owner, ref count);
        var requested = Math.Min(args.Shots, count.Count);
        if (requested < 1)
            return;

        var magazines = GetMagazineEntities(ent);
        var remaining = requested;
        var suppliedProjectiles = 0;

        // Projectile providers are consumed first so charge-only providers can be billed for
        // exactly the number of projectiles that were actually supplied.
        foreach (var (slotId, nested) in magazines)
        {
            if (nested is not { } uid)
                return;

            if (ent.Comp.Slots[slotId] is not null)
                continue;

            if (remaining <= 0)
                break;

            var take = new TakeAmmoEvent(remaining, new(), args.Coordinates, args.User)
            {
                FireCostMultiplier = args.FireCostMultiplier,
                SpawnProjectiles = args.SpawnProjectiles,
            };
            RaiseLocalEvent(uid, take);

            var supplied = Math.Min(take.Ammo.Count, remaining);
            for (var i = 0; i < supplied; i++)
                args.Ammo.Add(take.Ammo[i]);

            remaining -= supplied;
            suppliedProjectiles += supplied;
            if (take.Reason is not null && args.Reason is null)
                args.Reason = take.Reason;
        }

        if (suppliedProjectiles < 1)
            return;

        foreach (var (slotId, nested) in magazines)
        {
            if (nested is not { } uid)
                return;

            if (ent.Comp.Slots[slotId] is not { } multiplier)
                continue;

            var consume = new TakeAmmoEvent(suppliedProjectiles, new(), args.Coordinates, args.User)
            {
                FireCostMultiplier = multiplier,
                SpawnProjectiles = false,
            };
            RaiseLocalEvent(uid, consume);
        }

        UpdateMagazineAppearance(ent, magazines);
    }

    private void UpdateMagazineAppearance(Entity<MultiMagazineAmmoProviderComponent> ent,
        IReadOnlyDictionary<string, EntityUid?> magazines)
    {
        if (!TryComp<AppearanceComponent>(ent.Owner, out var appearance))
            return;

        var count = 0;
        var capacity = 0;
        var loaded = 0;
        var hasEffectiveCount = false;
        foreach (var (slotId, nested) in magazines)
        {
            if (nested is not { } uid)
                continue;

            loaded++;
            // Use the same effective counts as firing and GetAmmoCount. Raw appearance values
            // are incorrect for charge-only slots with a fire-cost multiplier.
            var nestedEvent = new GetAmmoCountEvent
            {
                FireCostMultiplier = ent.Comp.Slots[slotId] ?? 1f,
            };
            RaiseLocalEvent(uid, ref nestedEvent);

            if (!hasEffectiveCount)
            {
                count = nestedEvent.Count;
                capacity = nestedEvent.Capacity;
                hasEffectiveCount = true;
            }
            else
            {
                count = Math.Min(count, nestedEvent.Count);
                capacity = Math.Min(capacity, nestedEvent.Capacity);
            }
        }

        // A missing slot makes the composite unable to fire, matching OnGetAmmoCount.
        if (loaded != magazines.Count)
        {
            count = 0;
            capacity = 0;
        }

        _appearance.SetData(ent.Owner, AmmoVisuals.MagLoaded, loaded > 0, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.HasAmmo, count != 0, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.AmmoCount, count, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.AmmoMax, capacity, appearance);
    }
}
