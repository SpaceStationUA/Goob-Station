// Pirate: ported from ss14Starlight/space-station-14.
using Content.Server.Body.Systems;
using Content.Server._Pirate.StarlightSpecies.Pollen.Components;
using Content.Server.Actions;
using Content.Shared._Pirate.StarlightSpecies.Pollen;
using Content.Shared._Pirate.StarlightSpecies.Pollen.Components;
using Content.Server.Store.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Stack;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger.Systems;
using Content.Shared.Chemistry.EntitySystems;
//using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Objectives.Components;
using Content.Server.Pinpointer;
using Content.Server.Temperature.Components;

namespace Content.Server._Pirate.StarlightSpecies.Pollen;

/// <summary>
/// Owns the pollen perk shop: grants Dionas the action to open it, and runs
/// the effects of whatever perks they've bought. Collection and currency
/// granting live in PollenCollectorSystem - this system only cares about
/// what happens once points are spent.
/// </summary>
public sealed partial class PollenShopSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private NavMapSystem _navMap = default!;

    private static readonly EntProtoId<ObjectiveComponent> _pollenObjective = "PollenCollectionObjective";
    private static readonly EntProtoId _sporeCloudEmitter = "PollenSporeCloudEmitter";
    private static readonly EntProtoId _hardenStatusEffect = "PollenTreeBarkT2PassiveHardenEffect";
    private static readonly EntProtoId _pollenShopAction = "ActionOpenPollenShop";
    private static readonly EntProtoId _woodPlankStack10 = "MaterialWoodPlank10";
    private const string BloomingMossListingId = "PollenTreeFloralT2BloomingMoss";
    private static readonly EntProtoId _mossFireResistStatusEffect = "PollenTreeFloralT2PassiveMossFireResistEffect";
    private const string HardenListingId = "PollenTreeBarkT2Harden";
    private static readonly ProtoId<ReagentPrototype> _phytovitalin = "Phytovitalin";
    private const string AdvancedPollenListingId = "PollenTreeFloralT3AdvancedPollen";

    private const string AlertPollenListingId = "PollenTreeFloralT1AlertPollen";

    /// <summary>
    /// Listings that grant a periodic reagent drip when bought, keyed by
    /// listing id. Add an entry here for each new "drip" perk (e.g. Mushroom
    /// T3 Ultravasculine) instead of creating a new component/event per perk.
    /// </summary>
    private static readonly Dictionary<string, (ProtoId<ReagentPrototype> Reagent, float Amount, TimeSpan Interval)> _periodicReagentPerks = new()
    {
        ["PollenTreeBarkT3SapSerum"] = (_phytovitalin, 1f, TimeSpan.FromSeconds(60)),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, PollenCollectorInitializedEvent>(OnCollectorInitialized);
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnPurchaseCompleted);
        SubscribeLocalEvent<PollenCollectorComponent, PollenMakeWoodEffectEvent>(OnMakeWoodEffect);
        SubscribeLocalEvent<PollenCollectorComponent, PollenSporeCloudEffectEvent>(OnSporeCloudEffect);
        SubscribeLocalEvent<PollenCollectorComponent, PollenInjectPhytovitalinDoAfterEvent>(OnInjectPhytovitalinDoAfter);
        SubscribeLocalEvent<PollenAlertPollenComponent, MobStateChangedEvent>(OnAlertPollenMobStateChanged);
        SubscribeLocalEvent<PollenMossInsulationComponent, GetTemperatureProtectionEvent>(OnMossTemperatureProtection);
    }

    private void OnCollectorInitialized(Entity<PollenCollectorComponent> ent, ref PollenCollectorInitializedEvent args)
        => _actions.AddAction(ent.Owner, _pollenShopAction);

    private void OnPurchaseCompleted(ref StoreBuyFinishedEvent args)
    {
        // Pirate: native collective-mind channels replace Starlight's language radio relay.
        if (args.PurchasedItem.ID == "PollenTreeMushroomT2hivemind")
        {
            var mind = EnsureComp<Content.Shared._Starlight.CollectiveMind.CollectiveMindComponent>(args.StoreUid);
            mind.Channels.Add("Spore");
            mind.DefaultChannel ??= "Spore";
            Dirty(args.StoreUid, mind);
        }

        if (_periodicReagentPerks.TryGetValue(args.PurchasedItem.ID, out var config))
            {
                var perk = EnsureComp<PollenSerumPerkComponent>(args.StoreUid);
                perk.Reagent = config.Reagent;
                perk.Amount = config.Amount;
                perk.Interval = config.Interval;
                perk.NextTick = _timing.CurTime + config.Interval;
                return;
            }

        if (args.PurchasedItem.ID == HardenListingId)
        {
            GrantHardenArmor(args.StoreUid);
        }

        if (args.PurchasedItem.ID == AlertPollenListingId)
        {
            EnsureComp<PollenAlertPollenComponent>(args.StoreUid);
        }

        if (args.PurchasedItem.ID == BloomingMossListingId)
        {
            GrantMossInsulation(args.StoreUid);
            GrantMossFireResist(args.StoreUid);
        }

        if (args.PurchasedItem.ID == AdvancedPollenListingId)
        {
            var perk = EnsureComp<PollenAdvancedComponent>(args.StoreUid);
            perk.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(10);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var perkQuery = EntityQueryEnumerator<PollenSerumPerkComponent>();
        while (perkQuery.MoveNext(out var uid, out var perk))
        {
            if (now < perk.NextTick)
                continue;

            perk.NextTick = now + perk.Interval;
            AddSerum(uid, perk.Reagent, perk.Amount);
        }
    }

#region Floral
    // Floral

    // T1
    private void OnAlertPollenMobStateChanged(Entity<PollenAlertPollenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Alive || args.NewMobState != MobState.Critical)
            return;

        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } grid)
            return;

        var location = _navMap.GetNearestBeaconString((ent.Owner, xform), onlyName: true);
        var message = Loc.GetString("pollen-alert-pollen", ("location", location));

        _popup.PopupEntity(Loc.GetString("pollen-alert-pollen-self"), ent.Owner, ent.Owner, PopupType.LargeCaution);

        var query = EntityQueryEnumerator<PollenCollectorComponent, TransformComponent>();
        while (query.MoveNext(out var otherUid, out _, out var otherXform))
        {
            if (otherUid == ent.Owner || otherXform.GridUid != grid)
                continue;

            _popup.PopupEntity(message, otherUid, otherUid, PopupType.LargeCaution);
        }
    }

    // T2
    private void GrantMossInsulation(EntityUid buyer)
    {
        // Pirate: native temperature protection exposes an event instead of writable coefficients.
        EnsureComp<TemperatureProtectionComponent>(buyer);
        EnsureComp<PollenMossInsulationComponent>(buyer);
    }

    private void OnMossTemperatureProtection(Entity<PollenMossInsulationComponent> ent, ref GetTemperatureProtectionEvent args)
        => args.Coefficient *= 0.8f;

    private void GrantMossFireResist(EntityUid buyer)
        => _statusEffects.TrySetStatusEffectDuration(buyer, _mossFireResistStatusEffect);

    // T3

#endregion Floral

#region Bark

    private void OnMakeWoodEffect(Entity<PollenCollectorComponent> ent, ref PollenMakeWoodEffectEvent args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Slash", 20);
        _damageable.TryChangeDamage(ent.Owner, damage, ignoreResistances: true);

        var wood = SpawnAtPosition(_woodPlankStack10, Transform(ent.Owner).Coordinates);
        _hands.PickupOrDrop(ent.Owner, wood);
    }

    private void GrantHardenArmor(EntityUid buyer)
        => _statusEffects.TrySetStatusEffectDuration(buyer, _hardenStatusEffect);

    private void AddSerum(EntityUid uid, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream) ||
        _mobState.IsDead(uid))
        return;

        var solution = new Solution();
        solution.AddReagent(reagent, amount);
        _bloodstream.TryAddToBloodstream((uid, bloodstream), solution);
    }
#endregion Bark

#region Mushroom
    // T1
    private void OnInjectPhytovitalinDoAfter(Entity<PollenCollectorComponent> ent, ref PollenInjectPhytovitalinDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var actionUid = GetEntity(args.Action);

        if (args.Cancelled || args.Args.Target is not { } target)
        {
            _actions.ClearCooldown(actionUid);
            return;
        }

        var solution = new Solution();
        solution.AddReagent(_phytovitalin, 5f);

        var injected = false;

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            injected = _bloodstream.TryAddToBloodstream((target, bloodstream), solution);
        }
        else if (_solutionContainer.TryGetInjectableSolution(target, out var injectable, out _))
        {
            _solutionContainer.Inject(target, injectable.Value, solution);
            injected = true;
        }
        else if (_solutionContainer.TryGetRefillableSolution(target, out var refillable, out _))
        {
            _solutionContainer.Refill(target, refillable.Value, solution);
            injected = true;
        }

        if (!injected)
        {
            _popup.PopupEntity(Loc.GetString("pollen-inject-invalid-target"), ent.Owner, ent.Owner);
            _actions.ClearCooldown(actionUid);
            return;
        }

        var selfDamage = new DamageSpecifier();
        selfDamage.DamageDict.Add("Cellular", 16);
        selfDamage.DamageDict.Add("Bloodloss", 20);
        _damageable.TryChangeDamage(ent.Owner, selfDamage, ignoreResistances: true);
    }
    // T2

    // T3
    private void OnSporeCloudEffect(Entity<PollenCollectorComponent> ent, ref PollenSporeCloudEffectEvent args)
    {
        var emitter = SpawnAtPosition(_sporeCloudEmitter, Transform(ent.Owner).Coordinates);
        _trigger.Trigger(emitter, ent.Owner);
    }
#endregion Mushroom
}
