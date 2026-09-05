// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Cargo.Components;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Shared._Pirate.Forging;
using Content.Shared.Damage;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Server.Temperature.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Forging;

/// <summary>
/// Server-side metal behavior. Hot-hand damage is scheduled only while a hot item is held;
/// this system has no update loop and performs no entity-wide queries.
/// </summary>
public sealed class MetalSystem : SharedMetalSystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private readonly Dictionary<EntityUid, EntityUid> _holders = new();
    private readonly HashSet<EntityUid> _damageScheduled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetallicComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<MetallicComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<MetallicComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<MetallicComponent, ComponentShutdown>(OnMetallicShutdown);
        SubscribeLocalEvent<TemperatureComponent, ItemForgedEvent>(OnItemForged);
        SubscribeLocalEvent<TemperatureComponent, MetalWroughtEvent>(OnTemperatureWrought);
        SubscribeLocalEvent<ForgedItemComponent, ForgingCompletedEvent>(OnForgingCompleted);
        SubscribeLocalEvent<DestructibleComponent, ForgingWorkInitializedEvent>(OnWorkInitialized);
        SubscribeLocalEvent<DestructibleComponent, ForgingCompletedEvent>(OnDestructibleCompleted);
    }

    private void OnTemperatureChanged(Entity<MetallicComponent> ent, ref OnTemperatureChangeEvent args)
    {
        // Pirate: once a metal reaches its ideal temperature it stays workable until it
        // cools below the lower bound. Forge heating can cross the ideal in one step.
        if (ent.Comp.Workable)
        {
            if (args.CurrentTemperature < ent.Comp.MinTemp)
                SetWorkable(ent, false);
        }
        else if (args.CurrentTemperature >= ent.Comp.IdealTemp)
        {
            SetWorkable(ent, true);
        }

        if (_holders.TryGetValue(ent.Owner, out var holder) && args.CurrentTemperature >= ent.Comp.DamageHoldingTemp)
            ScheduleHoldingDamage(ent, holder);
    }

    private void OnEquippedHand(Entity<MetallicComponent> ent, ref GotEquippedHandEvent args)
    {
        _holders[ent.Owner] = args.User;
        if (TryComp<TemperatureComponent>(ent, out var temperature) &&
            temperature.CurrentTemperature >= ent.Comp.DamageHoldingTemp)
        {
            DealHoldingDamage(ent, args.User);
            ScheduleHoldingDamage(ent, args.User);
        }
    }

    private void OnUnequippedHand(Entity<MetallicComponent> ent, ref GotUnequippedHandEvent args)
    {
        _holders.Remove(ent.Owner);
    }

    private void OnMetallicShutdown(Entity<MetallicComponent> ent, ref ComponentShutdown args)
    {
        _holders.Remove(ent.Owner);
        _damageScheduled.Remove(ent.Owner);
    }

    private void ScheduleHoldingDamage(Entity<MetallicComponent> ent, EntityUid holder)
    {
        if (!_damageScheduled.Add(ent.Owner))
            return;

        var delay = ent.Comp.HoldingDamageInterval;
        Timer.Spawn(delay, () => HoldingDamageTimer(ent.Owner, holder));
    }

    private void HoldingDamageTimer(EntityUid uid, EntityUid holder)
    {
        _damageScheduled.Remove(uid);
        if (!_holders.TryGetValue(uid, out var currentHolder) || currentHolder != holder ||
            !TryComp<MetallicComponent>(uid, out var metallic) ||
            !TryComp<TemperatureComponent>(uid, out var temperature) ||
            temperature.CurrentTemperature < metallic.DamageHoldingTemp ||
            !_hands.IsHolding(holder, uid))
        {
            return;
        }

        DealHoldingDamage((uid, metallic), holder);
        ScheduleHoldingDamage((uid, metallic), holder);
    }

    private void DealHoldingDamage(Entity<MetallicComponent> ent, EntityUid holder)
    {
        var attempt = new DamageOnHoldingAttemptEvent(ent.Owner);
        RaiseLocalEvent(holder, ref attempt);
        if (attempt.Cancelled)
            return;

        _damage.TryChangeDamage(holder, ent.Comp.HoldingDamage, origin: ent.Owner);
    }

    private void OnItemForged(Entity<TemperatureComponent> ent, ref ItemForgedEvent args)
    {
        if (!TryComp<MetallicComponent>(ent, out var metallic) || metallic.Metal is not { } metalId ||
            !_prototypes.Resolve(metalId, out var metal))
        {
            return;
        }

        _temperature.ForceChangeTemperature(ent.Owner, metal.WorkingTemp, ent.Comp);
    }

    private void OnTemperatureWrought(Entity<TemperatureComponent> ent, ref MetalWroughtEvent args)
    {
        var resultTemperature = EnsureComp<TemperatureComponent>(args.Result);
        _temperature.ForceChangeTemperature(args.Result, ent.Comp.CurrentTemperature, resultTemperature);
    }

    private void OnForgingCompleted(Entity<ForgedItemComponent> ent, ref ForgingCompletedEvent args)
    {
        if (ent.Comp.Item != args.Item.ID || args.Item.Construction is not { } graph)
            return;

        if (!TryComp<ConstructionComponent>(ent, out var construction))
        {
            // Populate the state before adding the component so ComponentInit never observes
            // an empty graph and emits an invalid-construction warning.
            construction = new ConstructionComponent
            {
                Graph = graph,
                Node = "start",
                TargetNode = "finished",
                EdgeIndex = 0,
                StepIndex = 0,
            };
            AddComp(ent.Owner, construction);
            return;
        }

        construction.Graph = graph;
        construction.Node = "start";
        construction.TargetNode = "finished";
        construction.EdgeIndex = 0;
        construction.StepIndex = 0;
        Dirty(ent.Owner, construction);
    }

    private void OnWorkInitialized(Entity<DestructibleComponent> ent, ref ForgingWorkInitializedEvent args)
    {
        ScaleDamageThresholds(ent.Comp, args.Work.Double());
    }

    private void OnDestructibleCompleted(Entity<DestructibleComponent> ent, ref ForgingCompletedEvent args)
    {
        ScaleDamageThresholds(ent.Comp, args.Metal.Durability.Double());
    }

    private static void ScaleDamageThresholds(DestructibleComponent component, double scale)
    {
        foreach (var threshold in component.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger damage)
                damage.Damage *= scale;
        }
    }

    public override void SetPrice(EntityUid uid, double price)
    {
        var component = EnsureComp<StaticPriceComponent>(uid);
        component.Price = price;
    }
}
