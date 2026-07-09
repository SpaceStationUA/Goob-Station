using System.Linq;
using Content.Pirate.Shared.TerrorSpider.Evolving;
using Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;
using Content.Shared._Pirate.Weapons.Melee.Events;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Spider;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.TerrorSpider.Evolving.EntitySystems;

public abstract class SharedEvolvingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectivesSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolvingComponent, EvolveEvent>(OnEvolve);
        SubscribeLocalEvent<EvolvingComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<EvolvingComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<EvolvingComponent, AfterMeleeHitEvent>(AfterMeleeHit);
        SubscribeLocalEvent<EvolvingComponent, EggsInjectedEvent>(OnEggsInjected);
        SubscribeLocalEvent<EvolvingComponent, SpiderWebSpawnedEvent>(OnSpiderWebSpawn);
    }

    private void AfterMeleeHit(Entity<EvolvingComponent> ent, ref AfterMeleeHitEvent args)
    {
        if (!_timing.IsFirstTimePredicted || !args.IsHit || args.Handled || args.User != ent.Owner || args.HitEntities.Count <= 0)
            return;

        foreach (var condition in ent.Comp.Conditions)
        {
            if (condition is not DamageDealCondition damageDealCondition || damageDealCondition.Condition())
                continue;

            if (!damageDealCondition.OnlyAlive || args.HitEntities.All(entity => _mobStateSystem.IsAlive(entity)))
                damageDealCondition.AddDamage(args.DealedDamage.GetTotal().Float());
        }

        TryUpdateEvolveState(ent.Owner, ent.Comp, EvolveType.DamageDeal);
    }

    private void OnEggsInjected(Entity<EvolvingComponent> ent, ref EggsInjectedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var condition in ent.Comp.Conditions)
        {
            if (condition is EggsInjectCondition { } eggsInject && !eggsInject.Condition())
                eggsInject.UpdateEggs(1);
        }

        TryUpdateEvolveState(ent.Owner, ent.Comp, EvolveType.EggsInjected);
    }

    private void OnSpiderWebSpawn(Entity<EvolvingComponent> ent, ref SpiderWebSpawnedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var condition in ent.Comp.Conditions)
        {
            if (condition is SpiderWebCondition { } spiderWeb && !spiderWeb.Condition())
                spiderWeb.UpdateWebs(1);
        }

        TryUpdateEvolveState(ent.Owner, ent.Comp, EvolveType.SpiderWebsSpawned);
    }

    private void OnMindAdded(Entity<EvolvingComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Objectives.Count > 0)
        {
            foreach (var obj in ent.Comp.Objectives)
                _mindSystem.AddObjective(args.Mind.Owner, args.Mind.Comp, obj);
        }
        else
        {
            TryUpdateObjective(ent.Owner, ent.Comp, null, false);
        }
    }

    private void OnMindRemoved(Entity<EvolvingComponent> ent, ref MindRemovedMessage args) =>
        TryRemoveObjectives(args.Mind.Owner, args.Mind.Comp, ent.Comp, delete: false);

    private bool TryUpdateEvolveState(EntityUid uid, EvolvingComponent component, EvolveType? objType = null)
    {
        TryAddAction(uid, component);

        return objType != null && TryUpdateObjective(uid, component, objType, true);
    }

    private bool TryUpdateObjective(EntityUid uid, EvolvingComponent component, EvolveType? objType, bool increment)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return false;

        var objectivesToUpdate = new List<EntityUid>();

        foreach (var obj in mind.Objectives)
        {
            if (HasComp<EvolveConditionComponent>(obj))
                objectivesToUpdate.Add(obj);
        }

        if (objectivesToUpdate.Count == 0)
        {
            foreach (var condition in component.Conditions)
            {
                var objEnt = TryInitObjectives(mindId, mind, component.ObjectiveId, condition);

                if (!objEnt.IsValid())
                    continue;

                _mindSystem.AddObjective(mindId, mind, objEnt);
                component.Objectives.Add(objEnt);
            }
        }
        else if (increment)
        {
            foreach (var objective in objectivesToUpdate)
            {
                if (TryComp<EvolveConditionComponent>(objective, out var evolveCondition)
                    && (objType == null || evolveCondition.ConditionType == objType))
                    evolveCondition.Count += 1;
            }
        }

        return true;
    }

    public virtual EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition)
    {
        var obj = _objectivesSystem.TryCreateObjective(mindId, mind, objectiveId);
        if (obj is not { Valid: true } objEnt)
            return EntityUid.Invalid;

        if (TryComp<EvolveConditionComponent>(objEnt, out var evolveCondition))
            evolveCondition.ConditionType = condition.Type;

        return objEnt;
    }

    private bool TryAddAction(EntityUid uid, EvolvingComponent component)
    {
        if (component.EvolveActionEntity != null || !CanEvolve(uid, component))
            return false;

        component.EvolveActionEntity = _actionsSystem.AddAction(uid, component.EvolveActionId);

        return component.EvolveActionEntity != null;
    }

    private bool TryEvolve(EntityUid uid, EvolvingComponent component)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind) || !CanEvolve(uid, component))
            return false;

        TryRemoveObjectives(mindId, mind, component);

        var ent = EntityManager.PredictedSpawnAtPosition(component.EvolveTo, Transform(uid).Coordinates);
        _mindSystem.TransferTo(mindId, ent, mind: mind);
        QueueDel(uid);
        return true;
    }

    private bool TryRemoveObjectives(EntityUid mindId, MindComponent mind, EvolvingComponent component, bool delete = true)
    {
        var removedAny = false;
        foreach (var obj in component.Objectives)
        {
            if (TryRemoveObjective(mindId, mind, obj, delete))
                removedAny = true;
        }

        return removedAny;
    }

    private bool TryRemoveObjective(EntityUid mindId, MindComponent mind, EntityUid objective, bool delete)
    {
        var index = mind.Objectives.IndexOf(objective);
        if (index < 0)
            return false;

        if (delete)
            return _mindSystem.TryRemoveObjective(mindId, mind, index);

        mind.Objectives.RemoveAt(index);
        return true;
    }

    private void OnEvolve(Entity<EvolvingComponent> ent, ref EvolveEvent args) => TryEvolve(ent.Owner, ent.Comp);

    private bool CanEvolve(EntityUid uid, EvolvingComponent component) =>
        component.Conditions.All(c => c.Condition(new EvolvingConditionArgs(uid, component.EvolveActionEntity, EntityManager)));
}
