// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Ingestion;
using Content.Goobstation.Common.Medical;
using Content.Pirate.Shared.Ranching;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Ranching;

[RegisterComponent]
public sealed partial class HostileWhenUnhappyComponent : Component
{
    [DataField]
    public float HappinessRequired = -10f;

    [DataField(required: true)]
    public HTNCompoundTask UnhappyTask = default!;

    [DataField(required: true)]
    public HTNCompoundTask HappyTask = default!;
}

public sealed class RanchingHappinessSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly EntProtoId RawChickenMeat = "FoodMeatChicken";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly RanchingAgeingSystem _ageing = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HappinessComponent, MapInitEvent>(OnHappinessMapInit);
        SubscribeLocalEvent<HappinessComponent, InteractionSuccessEvent>(OnPetted);
        SubscribeLocalEvent<HappinessComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<HappinessComponent, BeforeVomitEvent>(OnVomit);
        SubscribeLocalEvent<HappinessComponent, AfterEatingEvent>(OnFavoriteFoodEaten);

        SubscribeLocalEvent<MostRecentlyEatenFoodTagsComponent, AfterEatingEvent>(OnFoodEaten);
        SubscribeLocalEvent<ChickenChestComponent, AfterEatingEvent>(OnChickenChestFoodEaten);
        SubscribeLocalEvent<VomitCounterComponent, BeforeVomitEvent>(OnVomitCounted);
        SubscribeLocalEvent<VomitCounterComponent, RanchingEggLaidEvent>(OnEggLaid);

        SubscribeLocalEvent<UnhappyWhenCrowdedComponent, MapInitEvent>(OnCrowdingMapInit);
        SubscribeLocalEvent<AddComponentOnHappyComponent, RanchingHappinessChangedEvent>(OnAddComponentThreshold);
        SubscribeLocalEvent<ReplaceOnUnhappyComponent, RanchingHappinessChangedEvent>(OnReplaceThreshold);
        SubscribeLocalEvent<HostileWhenUnhappyComponent, RanchingHappinessChangedEvent>(OnHostilityThreshold);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var happinessQuery = EntityQueryEnumerator<HappinessComponent>();
        while (happinessQuery.MoveNext(out var uid, out var happiness))
        {
            if (_mobState.IsDead(uid) || _timing.CurTime < happiness.NextUpdate)
                continue;

            happiness.NextUpdate = _timing.CurTime + UpdateInterval;
            ChangeHappiness((uid, happiness), happiness.RegenerationRate);
        }

        var crowdingQuery = EntityQueryEnumerator<UnhappyWhenCrowdedComponent>();
        while (crowdingQuery.MoveNext(out var uid, out var crowding))
        {
            if (_timing.CurTime < crowding.NextUpdate)
                continue;

            crowding.NextUpdate = _timing.CurTime + crowding.UpdateFrequency;
            var count = 0;
            foreach (var nearby in _lookup.GetEntitiesInRange(uid, crowding.Range))
            {
                if (_tag.HasTag(nearby, crowding.Tag))
                    count++;
            }

            if (count < crowding.MinEntities || !TryComp<HappinessComponent>(uid, out var happiness))
                continue;

            ChangeHappiness((uid, happiness), crowding.HappinessToDecrease);
        }
    }

    private void OnHappinessMapInit(Entity<HappinessComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Current = Math.Clamp(ent.Comp.Current, ent.Comp.Minimum, ent.Comp.Maximum);
        ent.Comp.NextUpdate = _timing.CurTime + UpdateInterval;
    }

    private void OnCrowdingMapInit(Entity<UnhappyWhenCrowdedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateFrequency;
    }

    private void OnPetted(Entity<HappinessComponent> ent, ref InteractionSuccessEvent args)
    {
        ChangeHappiness(ent, ent.Comp.HappinessIncrease);
    }

    private void OnDamaged(Entity<HappinessComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.Origin != null && args.Origin != ent.Owner)
            ChangeHappiness(ent, ent.Comp.DamageDecrease);
    }

    private void OnVomit(Entity<HappinessComponent> ent, ref BeforeVomitEvent args)
    {
        if (!args.Cancelled)
            ChangeHappiness(ent, ent.Comp.DamageDecrease);
    }

    private void OnFavoriteFoodEaten(Entity<HappinessComponent> ent, ref AfterEatingEvent args)
    {
        if (!TryComp<FavoriteFoodComponent>(ent, out var favorite) || !_tag.HasAnyTag(args.Food, favorite.Tag))
            return;

        ChangeHappiness(ent, favorite.Amount);
    }

    private void OnFoodEaten(Entity<MostRecentlyEatenFoodTagsComponent> ent, ref AfterEatingEvent args)
    {
        if (!TryComp<TagComponent>(args.Food, out var tags))
            return;

        ent.Comp.Tag.UnionWith(tags.Tags);

        // Pirate: Trauma makes chickens vomit after eating raw chicken meat.
        if (Prototype(args.Food)?.ID == RawChickenMeat.Id)
            _vomit.Vomit(ent.Owner);
    }

    private void OnChickenChestFoodEaten(Entity<ChickenChestComponent> ent, ref AfterEatingEvent args)
    {
        if (Prototype(args.Food) is not { } food)
            return;

        SpawnNextToOrDrop(food.ID, ent.Owner);
        SpawnNextToOrDrop(food.ID, ent.Owner);
    }

    private void OnVomitCounted(Entity<VomitCounterComponent> ent, ref BeforeVomitEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.TimesVomited++;
        if (ent.Comp.TimesVomited >= ent.Comp.NeededVomits)
            EnsureComp<VomitedEnoughMarkerComponent>(ent);
    }

    private void OnEggLaid(Entity<VomitCounterComponent> ent, ref RanchingEggLaidEvent args)
    {
        ent.Comp.TimesVomited = 0;
        RemComp<VomitedEnoughMarkerComponent>(ent);
    }

    private void OnAddComponentThreshold(Entity<AddComponentOnHappyComponent> ent,
        ref RanchingHappinessChangedEvent args)
    {
        if (args.NewValue < ent.Comp.HappinessRequired)
            return;

        EntityManager.AddComponents(ent.Owner, ent.Comp.Components);
        RemComp<AddComponentOnHappyComponent>(ent);
    }

    private void OnReplaceThreshold(Entity<ReplaceOnUnhappyComponent> ent,
        ref RanchingHappinessChangedEvent args)
    {
        if (args.NewValue > ent.Comp.HappinessRequired)
            return;

        var replacement = ent.Comp.Ent;
        RemComp<ReplaceOnUnhappyComponent>(ent);
        _ageing.TransformEntity(ent.Owner, replacement);
    }

    private void OnHostilityThreshold(Entity<HostileWhenUnhappyComponent> ent,
        ref RanchingHappinessChangedEvent args)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var desiredTask = args.NewValue < ent.Comp.HappinessRequired
            ? ent.Comp.UnhappyTask
            : ent.Comp.HappyTask;

        if (htn.RootTask == desiredTask)
            return;

        htn.RootTask = desiredTask;
        _htn.Replan(htn);
    }

    public void SetHappiness(Entity<HappinessComponent> ent, float value)
    {
        var oldValue = ent.Comp.Current;
        var newValue = Math.Clamp(value, ent.Comp.Minimum, ent.Comp.Maximum);
        if (MathHelper.CloseTo(oldValue, newValue))
            return;

        ent.Comp.Current = newValue;
        var ev = new RanchingHappinessChangedEvent(oldValue, newValue);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    public void ChangeHappiness(Entity<HappinessComponent> ent, float amount)
    {
        if (_mobState.IsDead(ent.Owner))
            return;

        SetHappiness(ent, ent.Comp.Current + amount);
    }
}
