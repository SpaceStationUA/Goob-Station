// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Cloning;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Silicons;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Owns skill storage, profile application, XP, and event relay.
/// </summary>
public sealed partial class SharedKnowledgeSystem : EntitySystem
{
    public static readonly string[] MasteryNames =
    [
        "knowledge-mastery-unskilled",
        "knowledge-mastery-average",
        "knowledge-mastery-advanced",
        "knowledge-mastery-expert",
        "knowledge-mastery-master",
    ];

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _network = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private EntityQuery<KnowledgeComponent> _knowledgeQuery;
    private EntityQuery<KnowledgeContainerComponent> _containerQuery;
    private EntityQuery<KnowledgeHolderComponent> _holderQuery;

    public readonly Dictionary<EntProtoId, KnowledgeComponent> AllKnowledges = new();

    public bool SkillsEnabled { get; private set; }
    public bool SkillGainEnabled { get; private set; }

    private const float LearnChance = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        _knowledgeQuery = GetEntityQuery<KnowledgeComponent>();
        _containerQuery = GetEntityQuery<KnowledgeContainerComponent>();
        _holderQuery = GetEntityQuery<KnowledgeHolderComponent>();

        Subs.CVar(_configuration, KnowledgeCVars.SkillsEnabled, value => SkillsEnabled = value, true);
        Subs.CVar(_configuration, KnowledgeCVars.SkillGain, value => SkillGainEnabled = value, true);

        SubscribeLocalEvent<KnowledgeContainerComponent, ComponentStartup>(OnContainerStartup);
        SubscribeLocalEvent<KnowledgeContainerComponent, ComponentShutdown>(OnContainerShutdown);
        // Pirate: initialize physical stores only when a brain is actually installed.
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<BrainComponent, BorgBrainInsertedEvent>(OnPhysicalBrainInserted);
        SubscribeLocalEvent<BrainComponent, BorgBrainRemovedEvent>(OnPhysicalBrainRemoved);
        SubscribeLocalEvent<BorgBrainComponent, BorgBrainInsertedEvent>(OnBorgWrapperInserted);
        SubscribeLocalEvent<BorgBrainComponent, BorgBrainRemovedEvent>(OnBorgWrapperRemoved);
        SubscribeLocalEvent<KnowledgeContainerComponent, OrganAddedToBodyEvent>(OnBrainAdded);
        SubscribeLocalEvent<KnowledgeContainerComponent, OrganRemovedFromBodyEvent>(OnBrainRemoved);
        SubscribeLocalEvent<KnowledgeContainerComponent, BorgBrainInsertedEvent>(OnBorgBrainInserted);
        SubscribeLocalEvent<KnowledgeContainerComponent, BorgBrainRemovedEvent>(OnBorgBrainRemoved);
        SubscribeLocalEvent<KnowledgeHolderComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<KnowledgeHolderComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<KnowledgeContainerComponent, TransferredToCloneEvent>(OnCloneTransfer);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        LoadKnowledgePrototypes();
    }

    private void OnContainerStartup(Entity<KnowledgeContainerComponent> ent, ref ComponentStartup args)
    {
        EnsureContainer(ent);
    }

    private void OnContainerShutdown(Entity<KnowledgeContainerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Container is { } container)
            _containers.ShutdownContainer(container);
    }

    private void OnOrganAdded(Entity<OrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Body) || !HasComp<BrainComponent>(ent.Owner))
            return;

        AdoptStore(args.Body, EnsureStore(ent.Owner));
    }

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.OldBody) ||
            !HasComp<BrainComponent>(ent.Owner) || !_containerQuery.TryComp(ent.Owner, out var store))
            return;

        UnlinkStore(args.OldBody, (ent.Owner, store));
    }

    private void OnPhysicalBrainInserted(Entity<BrainComponent> ent, ref BorgBrainInsertedEvent args)
    {
        if (!_timing.ApplyingState && !TerminatingOrDeleted(ent.Owner) && !TerminatingOrDeleted(args.Chassis))
            AdoptStore(args.Chassis, EnsureStore(ent.Owner));
    }

    private void OnPhysicalBrainRemoved(Entity<BrainComponent> ent, ref BorgBrainRemovedEvent args)
    {
        if (!_timing.ApplyingState && !TerminatingOrDeleted(ent.Owner) && !TerminatingOrDeleted(args.Chassis) &&
            _containerQuery.TryComp(ent.Owner, out var store))
            UnlinkStore(args.Chassis, (ent.Owner, store));
    }

    private void OnBorgWrapperInserted(Entity<BorgBrainComponent> ent, ref BorgBrainInsertedEvent args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Chassis))
            return;

        var store = FindPhysicalStore(ent.Owner) ?? EnsureStore(ent.Owner);
        AdoptStore(args.Chassis, store);
    }

    private void OnBorgWrapperRemoved(Entity<BorgBrainComponent> ent, ref BorgBrainRemovedEvent args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Chassis))
            return;

        var store = FindPhysicalStore(ent.Owner);
        if (store is { } physicalStore)
            UnlinkStore(args.Chassis, physicalStore);
    }

    private void OnBorgBrainInserted(Entity<KnowledgeContainerComponent> ent, ref BorgBrainInsertedEvent args)
    {
        if (!_timing.ApplyingState)
            AdoptStore(args.Chassis, ent);
    }

    private void OnBorgBrainRemoved(Entity<KnowledgeContainerComponent> ent, ref BorgBrainRemovedEvent args)
    {
        if (!_timing.ApplyingState)
            UnlinkStore(args.Chassis, ent);
    }

    private void OnBrainAdded(Entity<KnowledgeContainerComponent> ent, ref OrganAddedToBodyEvent args)
    {
        AdoptStore(args.Body, ent);
    }

    private void OnBrainRemoved(Entity<KnowledgeContainerComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        UnlinkStore(args.OldBody, ent);
    }

    private void OnMindAdded(Entity<KnowledgeHolderComponent> ent, ref MindAddedMessage args)
    {
        EnsureKnowledgeContainer(ent.Owner);
    }

    private void OnPolymorphed(Entity<KnowledgeHolderComponent> ent, ref PolymorphedEvent args)
    {
        if (ent.Owner == args.OldEntity)
            TransferKnowledge(ent.Owner, args.NewEntity);
    }

    private void OnCloneTransfer(Entity<KnowledgeContainerComponent> ent, ref TransferredToCloneEvent args)
    {
        TransferKnowledge(ent.Owner, args.Cloned);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>() || args.WasModified<KnowledgeCatalogPrototype>())
            LoadKnowledgePrototypes();
    }

    private void LoadKnowledgePrototypes()
    {
        AllKnowledges.Clear();
        foreach (var catalog in _prototypes.EnumeratePrototypes<KnowledgeCatalogPrototype>())
        {
            foreach (var id in catalog.Entries)
            {
                if (!_prototypes.TryIndex(id, out var prototype) ||
                    !prototype.TryGetComponent<KnowledgeComponent>(out var component, Factory))
                {
                    Log.Error($"Knowledge catalog {catalog.ID} contains invalid entry {id}.");
                    continue;
                }

                AllKnowledges[id] = component;
            }
        }
    }

    private Entity<KnowledgeContainerComponent> EnsureStore(EntityUid uid)
    {
        var component = EnsureComp<KnowledgeContainerComponent>(uid);
        EnsureContainer((uid, component));
        return (uid, component);
    }

    private Container EnsureContainer(Entity<KnowledgeContainerComponent> ent)
    {
        ent.Comp.Container ??= _containers.EnsureContainer<Container>(ent.Owner, KnowledgeContainerComponent.ContainerId);
        return ent.Comp.Container;
    }

    private Entity<KnowledgeContainerComponent>? FindPhysicalStore(EntityUid holder)
    {
        if (TerminatingOrDeleted(holder))
            return null;

        // MMI is a wrapper; its inserted brain is the physical knowledge store.
        if (TryComp<MMIComponent>(holder, out var mmi) && mmi.BrainSlot.Item is { } mmiBrain)
            return FindPhysicalStore(mmiBrain) ?? EnsureStore(mmiBrain);

        if (HasComp<BrainComponent>(holder) || HasComp<BorgBrainComponent>(holder))
            return EnsureStore(holder);

        if (TryComp<BodyComponent>(holder, out var body))
        {
            foreach (var (organ, _) in _body.GetBodyOrgans(holder, body))
            {
                if (HasComp<BrainComponent>(organ))
                    return EnsureStore(organ);
            }
        }

        if (TryComp<BorgChassisComponent>(holder, out var chassis) && chassis.BrainEntity is { } borgBrain)
            return FindPhysicalStore(borgBrain) ?? EnsureStore(borgBrain);

        return null;
    }

    private void AdoptStore(EntityUid holder, Entity<KnowledgeContainerComponent> store)
    {
        var holderComponent = EnsureComp<KnowledgeHolderComponent>(holder);
        if (holderComponent.KnowledgeEntity == store.Owner && store.Comp.Holder == holder)
            return;

        if (holderComponent.KnowledgeEntity is { } oldUid &&
            oldUid != store.Owner &&
            _containerQuery.TryComp(oldUid, out var oldStore))
        {
            MergeStores((oldUid, oldStore), store);
            oldStore.Holder = null;
            DirtyField(oldUid, oldStore, nameof(KnowledgeContainerComponent.Holder));
        }

        if (store.Comp.Holder is { } oldHolder && oldHolder != holder &&
            _holderQuery.TryComp(oldHolder, out var oldHolderComponent))
        {
            oldHolderComponent.KnowledgeEntity = null;
            Dirty(oldHolder, oldHolderComponent);
        }

        holderComponent.KnowledgeEntity = store.Owner;
        Dirty(holder, holderComponent);
        store.Comp.Holder = holder;
        DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Holder));
    }

    private void UnlinkStore(EntityUid holder, Entity<KnowledgeContainerComponent> store)
    {
        if (_holderQuery.TryComp(holder, out var holderComponent) && holderComponent.KnowledgeEntity == store.Owner)
        {
            holderComponent.KnowledgeEntity = null;
            Dirty(holder, holderComponent);
        }

        if (store.Comp.Holder == holder)
        {
            store.Comp.Holder = null;
            DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Holder));
        }
    }

    private void MergeStores(Entity<KnowledgeContainerComponent> source, Entity<KnowledgeContainerComponent> destination)
    {
        var destinationContainer = EnsureContainer(destination);

        foreach (var (id, sourceUid) in source.Comp.Knowledge.ToArray())
        {
            if (!_knowledgeQuery.TryComp(sourceUid, out var sourceKnowledge))
                continue;

            if (GetKnowledge(destination, id) is { } existing)
            {
                existing.Comp.LearnedLevel = Math.Max(existing.Comp.LearnedLevel, sourceKnowledge.LearnedLevel);
                existing.Comp.Experience = Math.Max(existing.Comp.Experience, sourceKnowledge.Experience);
                MergeEmployerBonus((sourceUid, sourceKnowledge), existing);
                Dirty(existing);
                PredictedQueueDel(sourceUid);
                continue;
            }

            _containers.Insert(sourceUid, destinationContainer);
            destination.Comp.Knowledge[id] = sourceUid;
        }

        source.Comp.Knowledge.Clear();
        DirtyField(source, source.Comp, nameof(KnowledgeContainerComponent.Knowledge));
        DirtyField(destination, destination.Comp, nameof(KnowledgeContainerComponent.Knowledge));
    }

    public Entity<KnowledgeContainerComponent> EnsureKnowledgeContainer(EntityUid holder)
    {
        if (_holderQuery.TryComp(holder, out var existingHolder) &&
            existingHolder.KnowledgeEntity is { } existingUid &&
            _containerQuery.TryComp(existingUid, out var existingStore))
            return (existingUid, existingStore);

        var store = FindPhysicalStore(holder) ?? EnsureStore(holder);
        AdoptStore(holder, store);
        return store;
    }

    public Entity<KnowledgeContainerComponent>? GetContainer(EntityUid uid)
    {
        if (_holderQuery.TryComp(uid, out var holder) &&
            holder.KnowledgeEntity is { } storeUid &&
            _containerQuery.TryComp(storeUid, out var store))
            return (storeUid, store);

        return _containerQuery.TryComp(uid, out var ownStore)
            ? (uid, ownStore)
            : null;
    }

    public void TransferKnowledge(EntityUid source, EntityUid destinationHolder)
    {
        if (GetContainer(source) is not { } sourceStore)
            return;

        var destinationStore = EnsureKnowledgeContainer(destinationHolder);
        if (sourceStore.Owner != destinationStore.Owner)
            MergeStores(sourceStore, destinationStore);
    }

    public Entity<KnowledgeComponent>? EnsureKnowledge(
        Entity<KnowledgeContainerComponent> store,
        EntProtoId id,
        int level = 0,
        bool popup = true)
    {
        if (!SkillsEnabled)
            return null;

        if (GetKnowledge(store, id) is { } existing)
        {
            var clamped = Math.Clamp(level, 0, 100);
            if (existing.Comp.LearnedLevel < clamped)
            {
                existing.Comp.LearnedLevel = clamped;
                Dirty(existing);
            }

            return existing;
        }

        if (!AllKnowledges.ContainsKey(id) ||
            !PredictedTrySpawnInContainer(id, store.Owner, KnowledgeContainerComponent.ContainerId, out var spawned) ||
            spawned is not { } unit ||
            !_knowledgeQuery.TryComp(unit, out var component))
        {
            Log.Error($"Failed to spawn knowledge {id} for {ToPrettyString(store)}.");
            return null;
        }

        component.LearnedLevel = Math.Clamp(level, 0, 100);
        Dirty(unit, component);
        store.Comp.Knowledge[id] = unit;
        DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Knowledge));

        if (store.Comp.Holder is not { } holder)
            return (unit, component);

        var added = new KnowledgeAddedEvent(store, holder);
        RaiseLocalEvent(unit, ref added);

        if (popup)
            SkillPopup(Loc.GetString("knowledge-unit-learned-popup", ("knowledge", Name(unit))), holder);

        return (unit, component);
    }

    public Entity<KnowledgeComponent>? RaiseMastery(
        Entity<KnowledgeContainerComponent> store,
        EntProtoId id,
        int mastery,
        bool popup = true)
    {
        if (EnsureKnowledge(store, id, popup: popup) is not { } knowledge)
            return null;

        var newMastery = GetMastery(knowledge.Comp.LearnedLevel) + mastery;
        knowledge.Comp.LearnedLevel = GetInverseMastery(newMastery);
        Dirty(knowledge);
        return knowledge;
    }

    public Entity<KnowledgeComponent>? GetKnowledge(EntityUid holder, EntProtoId id)
        => GetContainer(holder) is { } store ? GetKnowledge(store, id) : null;

    public void AddKnowledgeUnits(EntityUid holder, IReadOnlyDictionary<EntProtoId, int> skills, bool popup = false)
    {
        var store = EnsureKnowledgeContainer(holder);
        foreach (var (id, level) in skills)
            EnsureKnowledge(store, id, level, popup);
    }

    public Entity<KnowledgeComponent>? GetKnowledge(Entity<KnowledgeContainerComponent> store, EntProtoId id)
        => store.Comp.Knowledge.TryGetValue(id, out var uid) && _knowledgeQuery.TryComp(uid, out var component)
            ? (uid, component)
            : null;

    public int GetKnowledgeLevel(EntityUid holder, EntProtoId id)
        => GetKnowledge(holder, id)?.Comp.NetLevel ?? 0;

    /// <summary>
    /// Sets persistent progress for one skill without changing temporary bonuses.
    /// Intended for authoritative administrative and test tooling.
    /// </summary>
    public Entity<KnowledgeComponent>? SetKnowledgeProgress(
        EntityUid holder,
        EntProtoId id,
        int learnedLevel,
        int experience)
    {
        if (!SkillsEnabled || !AllKnowledges.ContainsKey(id))
            return null;

        var store = EnsureKnowledgeContainer(holder);
        if (EnsureKnowledge(store, id, popup: false) is not { } knowledge)
            return null;

        var level = Math.Clamp(learnedLevel, 0, 100);
        var maxExperience = level < 100 && knowledge.Comp.ExperienceCost > 0
            ? knowledge.Comp.ExperienceCost - 1
            : 0;

        knowledge.Comp.LearnedLevel = level;
        knowledge.Comp.Experience = Math.Clamp(experience, 0, maxExperience);
        knowledge.Comp.TimeToNextExperience = TimeSpan.Zero;
        Dirty(knowledge);

        if (store.Comp.Holder is { } actualHolder)
        {
            var changed = new KnowledgeExperienceChangedEvent();
            RaiseLocalEvent(actualHolder, ref changed);
        }

        return knowledge;
    }

    public List<Entity<KnowledgeComponent>>? GetAllKnowledge(EntityUid holder)
    {
        if (GetContainer(holder) is not { } store)
            return null;

        var result = new List<Entity<KnowledgeComponent>>(store.Comp.Knowledge.Count);
        foreach (var uid in store.Comp.Knowledge.Values)
        {
            if (_knowledgeQuery.TryComp(uid, out var component))
                result.Add((uid, component));
        }

        return result;
    }

    public List<Entity<T, KnowledgeComponent>>? GetKnowledgeWith<T>(EntityUid holder) where T : IComponent
    {
        if (GetContainer(holder) is not { } store)
            return null;

        var typed = GetEntityQuery<T>();
        var result = new List<Entity<T, KnowledgeComponent>>();
        foreach (var uid in store.Comp.Knowledge.Values)
        {
            if (typed.TryComp(uid, out var component) && _knowledgeQuery.TryComp(uid, out var knowledge))
                result.Add((uid, component, knowledge));
        }

        return result;
    }

    public EntityUid? RemoveKnowledge(EntityUid holder, EntProtoId id, bool force = false)
    {
        if (GetContainer(holder) is not { } store ||
            store.Comp.Holder is not { } actualHolder ||
            GetKnowledge(store, id) is not { } knowledge ||
            knowledge.Comp.Unremoveable && !force)
            return null;

        store.Comp.Knowledge.Remove(id);
        DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Knowledge));

        var removed = new KnowledgeRemovedEvent(store, actualHolder);
        RaiseLocalEvent(knowledge, ref removed);
        PredictedQueueDel(knowledge.Owner);
        SkillPopup(Loc.GetString("knowledge-unit-forgotten-popup", ("knowledge", Name(knowledge))), actualHolder);
        return holder;
    }

    public void ClearKnowledge(EntityUid holder, bool deleteEntities = true)
    {
        if (GetContainer(holder) is not { } store)
            return;

        var entities = store.Comp.Knowledge.Values.ToArray();
        store.Comp.Knowledge.Clear();
        DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Knowledge));

        if (!deleteEntities)
            return;

        foreach (var entity in entities)
            PredictedQueueDel(entity);
    }

    public void RelayEvent<T>(EntityUid holder, ref T args) where T : notnull
    {
        if (TryComp<MobStateComponent>(holder, out var mobState) && !_mobState.IsAlive(holder, mobState))
            return;

        if (GetContainer(holder) is not { } store)
            return;

        foreach (var unit in store.Comp.Knowledge.Values)
            RaiseLocalEvent(unit, ref args);
    }

    public void AddExperience(
        Entity<KnowledgeContainerComponent> store,
        EntProtoId id,
        int amount,
        int levelCap = 100,
        bool popup = true)
    {
        if (!SkillGainEnabled || amount <= 0)
            return;

        if (GetKnowledge(store, id) is not { } knowledge)
        {
            if (!AllKnowledges.TryGetValue(id, out var prototype) || prototype.Complex)
                return;

            if (PredictedRandom(store.Owner).NextDouble() < LearnChance)
                EnsureKnowledge(store, id, popup: popup);
            return;
        }

        if (store.Comp.Holder is { } holder)
            AddExperience(knowledge, holder, amount, levelCap);
    }

    public void AddExperience(Entity<KnowledgeComponent> knowledge, EntityUid holder, int amount, int levelCap = 100)
    {
        if (!SkillGainEnabled || amount <= 0 || knowledge.Comp.ExperienceCost <= 0)
            return;

        var cap = Math.Clamp(levelCap, 0, 100);
        if (_timing.CurTime < knowledge.Comp.TimeToNextExperience || knowledge.Comp.LearnedLevel >= cap)
            return;

        knowledge.Comp.TimeToNextExperience = _timing.CurTime + knowledge.Comp.TimeBetweenExperience;
        knowledge.Comp.Experience += amount + knowledge.Comp.BonusExperience;
        RollForLevelUp(knowledge, holder, cap);
        Dirty(knowledge);

        var changed = new KnowledgeExperienceChangedEvent();
        RaiseLocalEvent(holder, ref changed);
    }

    public bool RollForLevelUp(Entity<KnowledgeComponent> knowledge, EntityUid holder, int levelCap = 100)
    {
        var cost = knowledge.Comp.ExperienceCost;
        var cap = Math.Clamp(levelCap, 0, 100);
        if (cost <= 0 || knowledge.Comp.Experience < cost || knowledge.Comp.LearnedLevel >= cap)
            return false;

        var oldMastery = GetMastery(knowledge.Comp.NetLevel);
        var rolls = knowledge.Comp.Experience / cost;
        knowledge.Comp.Experience -= rolls * cost;

        for (var i = 0; i < rolls && knowledge.Comp.LearnedLevel < cap; i++)
        {
            var (increase, _) = RollPenetrating(knowledge);
            knowledge.Comp.LearnedLevel = Math.Min(knowledge.Comp.LearnedLevel + increase, cap);
        }

        Dirty(knowledge);
        if (oldMastery != GetMastery(knowledge.Comp.NetLevel))
        {
            SkillPopup(Loc.GetString(
                "knowledge-level-up-popup",
                ("knowledge", Name(knowledge)),
                ("mastery", GetMasteryString(knowledge).ToLowerInvariant())), holder);
        }

        return true;
    }

    public (int Increase, bool Critical) RollPenetrating(Entity<KnowledgeComponent> knowledge)
    {
        var random = PredictedRandom(knowledge.Owner);
        var sides = DieSides(knowledge.Comp);
        var current = random.Next(1, sides + 1);
        var total = current;
        var critical = false;

        for (var penetrations = 0; current == sides && penetrations < 10; penetrations++)
        {
            sides = DieSides(knowledge.Comp, penetrations / 2);
            current = random.Next(1, sides + 1);
            total += current - 1;
            critical = true;
        }

        return (total, critical);
    }

    private int DieSides(KnowledgeComponent knowledge, int shift = 0)
        => GetMastery(knowledge.NetLevel) + shift switch
        {
            >= 4 => 3,
            >= 3 => 4,
            >= 2 => 6,
            >= 1 => 8,
            _ => 12,
        };

    private System.Random PredictedRandom(EntityUid entity)
    {
        var seed = SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(entity).Id,
            0x534B494C);
        return new System.Random(seed);
    }

    public static int GetMastery(int level)
        => level switch
        {
            >= 100 => 5,
            >= 88 => 4,
            >= 75 => 3,
            >= 50 => 2,
            >= 25 => 1,
            _ => 0,
        };

    public static int GetInverseMastery(int mastery)
        => mastery switch
        {
            >= 5 => 100,
            >= 4 => 88,
            >= 3 => 75,
            >= 2 => 50,
            >= 1 => 25,
            _ => 0,
        };

    public static string GetMasteryString(int mastery)
        => Robust.Shared.Localization.Loc.GetString(
            MasteryNames[Math.Clamp(mastery, 0, MasteryNames.Length - 1)]);

    public static string GetMasteryString(Entity<KnowledgeComponent> knowledge)
        => GetMasteryString(GetMastery(knowledge.Comp.NetLevel));

    public static float SharpCurve(int level, int offset = 0, float inverseScale = 100f)
    {
        var linear = (level + offset) / inverseScale;
        return linear * linear;
    }

    public float SharpCurve(Entity<KnowledgeComponent> knowledge, int offset = 0, float inverseScale = 100f)
        => SharpCurve(knowledge.Comp.NetLevel, offset, inverseScale);

    public int[]? SkillCosts(EntProtoId id)
        => AllKnowledges.TryGetValue(id, out var component) ? component.Costs : null;

    public int? SkillCost(EntProtoId id, int mastery)
        => SkillCosts(id) is { } costs && mastery >= 0 && mastery < costs.Length
            ? costs[mastery]
            : null;

    public int ProfileCost(KnowledgeProfile profile)
    {
        if (profile.Mastery is null)
            return 0;

        var total = 0;
        foreach (var (id, mastery) in profile.Mastery)
            total += SkillCost(id, mastery) ?? 0;
        return total;
    }

    public void EnsureProfileValid(ProtoId<KnowledgeProfilePrototype> parentId, ref KnowledgeProfile profile)
    {
        var parent = _prototypes.Index<KnowledgeProfilePrototype>(parentId);
        profile.Mastery ??= new Dictionary<EntProtoId, int>();

        foreach (var (id, increase) in profile.Mastery.ToArray())
        {
            var racialBase = parent.Profile.Mastery?.GetValueOrDefault(id) ?? 0;
            if (increase < 0 || SkillCost(id, increase) is null ||
                SkillCosts(id) is not { } costs || racialBase + increase >= costs.Length)
                profile.Mastery.Remove(id);
        }

        while (ProfileCost(profile) > parent.PointsLimit && profile.Mastery.Count > 0)
        {
            var remove = profile.Mastery
                .OrderByDescending(pair => SkillCost(pair.Key, pair.Value) ?? 0)
                .ThenByDescending(pair => pair.Key.Id)
                .First().Key;
            profile.Mastery.Remove(remove);
        }
    }

    public void ApplyProfile(EntityUid holder, ProtoId<KnowledgeProfilePrototype> parentId, KnowledgeProfile profile)
    {
        var store = EnsureKnowledgeContainer(holder);
        ClearKnowledge(store.Owner);

        var parent = _prototypes.Index<KnowledgeProfilePrototype>(parentId);
        ApplyProfile(store, parent.Profile);

        EnsureProfileValid(parentId, ref profile);
        ApplyProfile(store, profile, parent.PointsLimit);
    }

    public void ApplyProfile(Entity<KnowledgeContainerComponent> store, KnowledgeProfile profile)
    {
        if (profile.Mastery is null)
            return;

        foreach (var (id, mastery) in profile.Mastery)
            RaiseMastery(store, id, mastery, popup: false);
    }

    public void ApplyProfile(Entity<KnowledgeContainerComponent> store, KnowledgeProfile profile, int points)
    {
        if (profile.Mastery is null)
            return;

        foreach (var (id, mastery) in profile.Mastery.OrderBy(pair => pair.Key.Id))
        {
            if (SkillCost(id, mastery) is not { } cost || cost > points)
                continue;

            if (RaiseMastery(store, id, mastery, popup: false) is not null)
                points -= cost;
        }
    }

    public Dictionary<EntProtoId, int> GetSkillMasteries(EntityUid holder)
    {
        var result = new Dictionary<EntProtoId, int>();
        if (GetContainer(holder) is not { } store)
            return result;

        foreach (var (id, uid) in store.Comp.Knowledge)
            result[id] = _knowledgeQuery.TryComp(uid, out var component) ? GetMastery(component.NetLevel) : 0;
        return result;
    }

    public KnowledgeInfo GetKnowledgeInfo(Entity<KnowledgeComponent> knowledge)
    {
        var meta = MetaData(knowledge);
        return new KnowledgeInfo(
            meta.EntityName,
            meta.EntityDescription,
            Loc.GetString("knowledge-info-description",
                ("level", knowledge.Comp.NetLevel),
                ("mastery", GetMasteryString(knowledge))),
            knowledge.Comp.Color,
            knowledge.Comp.Sprite,
            knowledge.Comp.LearnedLevel,
            knowledge.Comp.NetLevel,
            knowledge.Comp.Experience,
            knowledge.Comp.ExperienceCost);
    }

    public void SkillPopup(string popup, EntityUid user)
    {
        var message = new SkillPopupEvent(popup);
        if (_network.IsServer)
            RaiseNetworkEvent(message, user);
        else if (_players.LocalEntity == user && _timing.IsFirstTimePredicted)
            RaiseLocalEvent(message);
    }
}
