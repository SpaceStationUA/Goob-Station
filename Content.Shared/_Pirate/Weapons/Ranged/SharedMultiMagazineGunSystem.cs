// SPDX-License-Identifier: AGPL-3.0-or-later

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
        var magazines = new List<EntityUid>();
        foreach (var nested in GetMagazineEntities(ent).Values)
        {
            if (nested is not { } uid)
                return;

            RaiseLocalEvent(uid, args);
            magazines.Add(uid);
        }

        _gun.UpdateAmmoCount(ent.Owner);
        UpdateMagazineAppearance(ent, magazines);
    }

    private void OnGetVerbs(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var magazines = new List<EntityUid>();
        foreach (var nested in GetMagazineEntities(ent).Values)
        {
            if (nested is not { } uid)
                return;

            RaiseLocalEvent(uid, args);
            magazines.Add(uid);
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

        var magazines = new List<EntityUid>();
        foreach (var nested in GetMagazineEntities(ent).Values)
        {
            if (nested is { } uid)
                magazines.Add(uid);
        }

        if (TryComp<AppearanceComponent>(ent.Owner, out var appearance))
            _appearance.SetData(ent.Owner, AmmoVisuals.MagLoaded, magazines.Count > 0, appearance);

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
        if (count.Count < 1)
            return;

        foreach (var (slotId, nested) in GetMagazineEntities(ent))
        {
            if (nested is not { } uid)
                return;

            if (ent.Comp.Slots[slotId] is not { } multiplier)
            {
                RaiseLocalEvent(uid, args);
                continue;
            }

            var consume = new TakeAmmoEvent(args.Shots, new(), args.Coordinates, args.User)
            {
                FireCostMultiplier = multiplier,
                SpawnProjectiles = false,
            };
            RaiseLocalEvent(uid, consume);
        }
    }

    private void UpdateMagazineAppearance(Entity<MultiMagazineAmmoProviderComponent> ent,
        IReadOnlyCollection<EntityUid> magazines)
    {
        if (!TryComp<AppearanceComponent>(ent.Owner, out var appearance))
            return;

        var count = 0;
        var capacity = 0;
        foreach (var uid in magazines)
        {
            if (!TryComp<AppearanceComponent>(uid, out var nestedAppearance))
                continue;

            _appearance.TryGetData<int>(uid, AmmoVisuals.AmmoCount, out var nestedCount, nestedAppearance);
            _appearance.TryGetData<int>(uid, AmmoVisuals.AmmoMax, out var nestedCapacity, nestedAppearance);
            count += nestedCount;
            capacity += nestedCapacity;
        }

        _appearance.SetData(ent.Owner, AmmoVisuals.MagLoaded, magazines.Count > 0, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.HasAmmo, count != 0, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.AmmoCount, count, appearance);
        _appearance.SetData(ent.Owner, AmmoVisuals.AmmoMax, capacity, appearance);
    }
}
