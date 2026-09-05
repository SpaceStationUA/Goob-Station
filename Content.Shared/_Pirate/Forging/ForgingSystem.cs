// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.Durability;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Forging;

/// <summary>
/// Creates forged items and applies their material statistics. It has no per-frame work.
/// </summary>
public sealed class ForgingSystem : EntitySystem
{
    public static readonly EntProtoId UnfinishedItem = "UnfinishedForgedItem";
    public static readonly EntProtoId DefaultResult = "ForgedPart";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DurabilitySystem _durability = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMetalSystem _metal = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly WorkableSystem _workable = default!;

    private EntityQuery<ForgedItemComponent> _forgedQuery;

    public readonly Dictionary<ForgingCategoryPrototype, List<ForgedItemPrototype>> AllItems = new();

    public override void Initialize()
    {
        base.Initialize();

        _forgedQuery = GetEntityQuery<ForgedItemComponent>();
        SubscribeLocalEvent<ForgedItemComponent, MetalWroughtEvent>(OnWrought);
        SubscribeLocalEvent<MeleeWeaponComponent, ForgingCompletedEvent>(OnMeleeCompleted);
        SubscribeLocalEvent<DamageOtherOnHitComponent, ForgingCompletedEvent>(OnThrownDamageCompleted);
        SubscribeLocalEvent<IncreaseDamageOnWieldComponent, ForgingCompletedEvent>(OnWieldDamageCompleted);
        SubscribeLocalEvent<ProjectileComponent, ForgingCompletedEvent>(OnProjectileCompleted);
        SubscribeLocalEvent<ToolComponent, ForgingCompletedEvent>(OnToolCompleted);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadPrototypes();
    }

    private void OnWrought(Entity<ForgedItemComponent> ent, ref MetalWroughtEvent args)
    {
        var metalId = _metal.GetMetalOrThrow(ent.Owner);
        var item = _prototypes.Index(ent.Comp.Item);
        var metal = _prototypes.Index(metalId);

        _metal.SetMetal(args.Result, metalId);
        SetItemPrototype(args.Result, ent.Comp.Item, completed: true);
        MakeOverheatable(args.Result, metal, completed: true);

        // Pirate: quality is rolled by ForgingCompletedEvent, so establish the base price first.
        if (item.Result is not null)
            SetPrice(args.Result, metal, item);

        ModifyResult(args.Result, args.User, metal, item, item.DisplayName(_prototypes));

        if (item.Tag is { } tag)
            _metal.AddUnworkableTag(args.Result, tag);
    }

    private void OnMeleeCompleted(Entity<MeleeWeaponComponent> ent, ref ForgingCompletedEvent args)
    {
        if (args.Metal.Speed != 1f)
        {
            ent.Comp.AttackRate *= args.Metal.Speed;
            DirtyField(ent, ent.Comp, nameof(MeleeWeaponComponent.AttackRate));
        }

        if (args.Metal.Damage.Count == 0 && args.Metal.DamageBonus.Count == 0)
            return;

        ModifyDamage(ent.Comp.Damage.DamageDict, args.Metal);
        DirtyField(ent, ent.Comp, nameof(MeleeWeaponComponent.Damage));
    }

    private void OnThrownDamageCompleted(Entity<DamageOtherOnHitComponent> ent, ref ForgingCompletedEvent args)
    {
        if (args.Metal.Damage.Count == 0 && args.Metal.DamageBonus.Count == 0)
            return;

        ModifyDamage(ent.Comp.Damage.DamageDict, args.Metal);
    }

    private void OnWieldDamageCompleted(Entity<IncreaseDamageOnWieldComponent> ent, ref ForgingCompletedEvent args)
    {
        if (args.Metal.Damage.Count == 0 && args.Metal.DamageBonus.Count == 0)
            return;

        ModifyDamage(ent.Comp.BonusDamage.DamageDict, args.Metal);
    }

    private void OnProjectileCompleted(Entity<ProjectileComponent> ent, ref ForgingCompletedEvent args)
    {
        if (args.Metal.Damage.Count == 0 && args.Metal.DamageBonus.Count == 0)
            return;

        ModifyDamage(ent.Comp.Damage.DamageDict, args.Metal);
        Dirty(ent);
    }

    private void OnToolCompleted(Entity<ToolComponent> ent, ref ForgingCompletedEvent args)
    {
        if (args.Metal.Speed == 1f)
            return;

        ent.Comp.SpeedModifier *= args.Metal.Speed;
        Dirty(ent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ForgedItemPrototype>() || args.WasModified<ForgingCategoryPrototype>())
            LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        AllItems.Clear();
        foreach (var category in _prototypes.EnumeratePrototypes<ForgingCategoryPrototype>())
            AllItems[category] = new List<ForgedItemPrototype>();

        foreach (var item in _prototypes.EnumeratePrototypes<ForgedItemPrototype>())
        {
            if (item.Abstract)
                continue;

            if (!AllItems.TryGetValue(_prototypes.Index(item.Category), out var items))
                continue;
            items.Add(item);
        }

        foreach (var items in AllItems.Values)
            items.Sort((first, second) => string.Compare(first.DisplayName(_prototypes), second.DisplayName(_prototypes), StringComparison.Ordinal));
    }

    public static void ModifyDamage(
        Dictionary<string, FixedPoint2> damage,
        MetalPrototype metal)
    {
        var baseTotal = FixedPoint2.Zero;
        foreach (var (type, modifier) in metal.Damage)
        {
            if (!damage.TryGetValue(type, out var current))
                continue;

            baseTotal += current;
            damage[type] = current * modifier;
        }

        foreach (var (type, modifier) in metal.DamageBonus)
        {
            var bonus = baseTotal * modifier;
            damage[type] = damage.TryGetValue(type, out var current) ? current + bonus : bonus;
        }
    }

    public EntityUid SpawnUnfinished(
        EntityCoordinates coordinates,
        ProtoId<MetalPrototype> metalId,
        ProtoId<ForgedItemPrototype> itemId,
        FixedPoint2 workScale)
    {
        var uid = PredictedSpawnAtPosition(UnfinishedItem, coordinates);
        _transform.SetLocalRotation(uid, Angle.Zero);

        var metal = _prototypes.Index(metalId);
        var item = _prototypes.Index(itemId);
        _metal.SetMetal(uid, metalId);
        SetItemPrototype(uid, itemId);
        MakeOverheatable(uid, metal);
        _metaData.SetEntityName(uid,
            Loc.GetString("forging-unfinished-name", ("metal", metal.Name), ("item", item.DisplayName(_prototypes))));

        var workable = Comp<WorkableComponent>(uid);
        var work = item.Work * metal.WorkScale * workScale;
        _workable.SetRemaining((uid, workable), work);
        _workable.SetResult((uid, workable), item.Result ?? DefaultResult);
        _workable.SetAmount((uid, workable), item.Amount);

        var initialized = new ForgingWorkInitializedEvent(work);
        RaiseLocalEvent(uid, ref initialized);
        return uid;
    }

    public void SetItemPrototype(EntityUid uid, ProtoId<ForgedItemPrototype> item, bool completed = false)
    {
        var component = EnsureComp<ForgedItemComponent>(uid);
        component.Item = item;
        component.Completed = completed;
        Dirty(uid, component);

        var ev = new ItemForgedEvent(item);
        RaiseLocalEvent(uid, ref ev);
    }

    public void MakeOverheatable(EntityUid uid, MetalPrototype metal, bool completed = false)
    {
        var component = EnsureComp<BurnableForgedComponent>(uid);
        component.BurnTemp = completed ? metal.MeltTemp : metal.MaxTemp;
        component.BurnedPrototype = metal.Overheated;
        component.BurnedPrefix = "heated-name-text";
        component.BurnedPopup = completed ? "metal-melted-popup" : "workable-metal-overheat-popup";
    }

    public void ModifyResult(
        EntityUid uid,
        EntityUid? user,
        MetalPrototype metal,
        ForgedItemPrototype item,
        string itemName)
    {
        _metaData.SetEntityName(uid,
            Loc.GetString("forging-result-name", ("metal", metal.Name), ("item", itemName)));

        _durability.SetScale(uid, metal.Durability);
        var ev = new ForgingCompletedEvent(metal, item, uid, user);
        RaiseLocalEvent(uid, ref ev);
        if (user is { } actualUser)
            RaiseLocalEvent(actualUser, ref ev);
    }

    public EntityUid? FinishForgedItem(EntityUid part, EntityUid? user)
    {
        if (!_forgedQuery.TryComp(part, out var component))
        {
            Log.Error($"Tried to finish non-forged item {ToPrettyString(part)}.");
            return null;
        }

        var item = _prototypes.Index(component.Item);
        if (item.Finished is not { } finished)
        {
            Log.Error($"Forged item {component.Item} has no finished prototype.");
            return null;
        }

        var wasHolding = user is { } holder && _hands.IsHolding(holder, part);
        var transform = Transform(part);
        var rotation = transform.LocalRotation;
        var result = Spawn(finished, transform.Coordinates);
        var metal = _prototypes.Index(_metal.GetMetalOrThrow(part));
        _metal.SetMetal(result, metal.ID);
        SetPrice(result, metal, item);

        // Transfer after setting the base price, but before ForgingCompleted is raised. This
        // applies price quality exactly once and prevents a second quality roll on the result.
        var qualityTransfer = new QualityTransferEvent(result);
        RaiseLocalEvent(part, ref qualityTransfer);
        MakeOverheatable(result, metal, completed: true);
        ModifyResult(result, user, metal, item, Name(result));
        QueueDel(part);

        _transform.AttachToGridOrMap(result);
        _transform.SetLocalRotation(result, rotation);
        if (wasHolding)
            _hands.TryPickupAnyHand(user!.Value, result);

        return result;
    }

    public bool CanMakeFrom(ForgedItemPrototype item, ProtoId<MetalPrototype> metal)
        => item.Whitelist?.Contains(metal) != false && item.Blacklist?.Contains(metal) != true;

    public string GetDisplayName(ForgedItemPrototype item)
        => item.DisplayName(_prototypes);

    private void SetPrice(EntityUid uid, MetalPrototype metal, ForgedItemPrototype item)
    {
        var itemWork = item.Work * metal.WorkScale / Math.Max(1, item.Amount);
        _metal.SetPrice(uid, (metal.Price * itemWork * item.Cost).Double());
    }
}
