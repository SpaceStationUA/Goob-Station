using System.Linq;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server._Pirate.Antag;
using Content.Server.GameTicking;
using Content.Server._Pirate.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Antag;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Preferences;
using Content.Server.Preferences.Managers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Pirate.Server.SecretTP.Components;

namespace Content.Pirate.Server.SecretTP;

public sealed class SecretTPSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _pendingDeaths = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SecretTPComponent, ComponentStartup>(OnBudgetStartup);
        SubscribeLocalEvent<SecretTPComponent, ComponentShutdown>(OnBudgetShutdown);
        SubscribeLocalEvent<SecretTPComponent, ComponentRemove>(OnBudgetRemove);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _pendingDeaths.Clear());
        SubscribeLocalEvent<PirateGameRuleAddAttemptEvent>(OnGameRuleAddAttempt);
        SubscribeLocalEvent<GameRuleAddedEvent>(OnGameRuleAdded);
        SubscribeLocalEvent<GameRuleEndedEvent>(OnGameRuleEnded);
        SubscribeLocalEvent<PirateSecretPlusPrimarySelectionEvent>(OnPrimarySelection);
        SubscribeLocalEvent<PirateAntagAssignmentAttemptEvent>(OnAntagAssignmentAttempt);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagEntitySelected);
        SubscribeLocalEvent<PirateSecretPlusRuleFilterEvent>(OnRuleFilter);
        SubscribeLocalEvent<PirateSecretPlusRuleStartedEvent>(OnRuleStarted);
    }

    private void OnGameRuleAddAttempt(ref PirateGameRuleAddAttemptEvent ev)
    {
        if (!TryGetActiveBudget(out var budget) || ev.RuleId == SecretTPConstants.RuleId)
            return;

        if (budget.RuleBlacklist.Contains(new ProtoId<EntityPrototype>(ev.RuleId)))
        {
            ev.Cancelled = true;
            ev.RejectionReason = Loc.GetString("cmd-secrettp-rule-blacklisted", ("rule", ev.RuleId));
            return;
        }

        var missingDepartments = GetMissingDepartmentRequirements(budget, new ProtoId<EntityPrototype>(ev.RuleId));
        if (missingDepartments.Count > 0)
        {
            ev.Cancelled = true;
            ev.RejectionReason = FormatMissingDepartmentRequirementsLocalized(ev.RuleId, missingDepartments);
            return;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(ev.RuleId, out var prototype))
            return;

        var cost = EstimateRuleCost(budget, prototype, 0);
        if (cost <= 0)
            return;

        var available = GetAvailablePoints(budget);
        if (cost > available)
        {
            ev.Cancelled = true;
            ev.RejectionReason = Loc.GetString("cmd-secrettp-not-enough-points",
                ("required", cost),
                ("available", available));
            return;
        }

        if (!budget.PendingRuleReservations.TryGetValue(ev.RuleId, out var pending))
        {
            pending = new Queue<int>();
            budget.PendingRuleReservations[ev.RuleId] = pending;
        }

        pending.Enqueue(cost);
        UpdateReservedPoints(budget);
    }

    private void OnPrimarySelection(PirateSecretPlusPrimarySelectionEvent ev)
    {
        if (!TryComp<SecretTPComponent>(ev.Scheduler, out var budget))
            return;

        var total = budget.GreenShiftWeight + budget.RedShiftWeight;
        if (total <= 0)
        {
            ev.Cancelled = true;
            return;
        }

        var roll = _random.NextFloat(0f, total);
        if (roll < budget.GreenShiftWeight)
            ev.Cancelled = true;
    }

    private void OnAntagAssignmentAttempt(ref PirateAntagAssignmentAttemptEvent ev)
    {
        if (!TryGetActiveBudget(out var budget))
            return;

        var ruleId = MetaData(ev.Rule).EntityPrototype?.ID;
        if (ruleId is not null)
        {
            if (budget.RuleBlacklist.Contains(new ProtoId<EntityPrototype>(ruleId)))
            {
                ev.Cancelled = true;
                ev.RejectionReason = Loc.GetString("cmd-secrettp-rule-blacklisted", ("rule", ruleId));
                return;
            }

            var missingDepartments = GetMissingDepartmentRequirements(budget, new ProtoId<EntityPrototype>(ruleId));
            if (missingDepartments.Count > 0)
            {
                ev.Cancelled = true;
                ev.RejectionReason = FormatMissingDepartmentRequirementsLocalized(ruleId, missingDepartments);
                return;
            }
        }

        var cost = GetDefinitionAntagCost(budget, ev.Definition);
        if (cost <= 0)
            return;

        RecalculateTotal(budget);
        ProcessExpiredReservations(budget);

        var currentReservation = budget.Reservations.GetValueOrDefault(ev.Rule);
        var otherReservations = budget.Reservations.Values.Sum() - currentReservation;
        var pendingReservations = budget.PendingRuleReservations.Values.SelectMany(x => x).Sum();
        var available = Math.Max(0, budget.TotalPoints - GetUsedPoints(budget) -
            otherReservations - pendingReservations);

        if (cost > available)
        {
            ev.Cancelled = true;
            ev.RejectionReason = Loc.GetString("cmd-secrettp-not-enough-points",
                ("required", cost),
                ("available", available));
        }
    }

    private void OnAntagEntitySelected(ref AfterAntagEntitySelectedEvent ev)
    {
        if (ev.Session is null)
            return;

        if (!TryGetActiveBudget(out var budget) ||
            !budget.Reservations.TryGetValue(ev.GameRule.Owner, out var reservation))
            return;

        var cost = GetDefinitionAntagCost(budget, ev.Def);
        if (cost <= 0)
            return;

        budget.Reservations[ev.GameRule.Owner] = Math.Max(0, reservation - cost);
        UpdateReservedPoints(budget);
    }

    private void OnGameRuleAdded(ref GameRuleAddedEvent ev)
    {
        if (!TryGetActiveBudget(out var budget) || ev.RuleId == SecretTPConstants.RuleId)
            return;

        if (budget.PendingRuleReservations.TryGetValue(ev.RuleId, out var pending) && pending.Count > 0)
        {
            budget.Reservations[ev.RuleEntity] = pending.Dequeue();
            if (pending.Count == 0)
                budget.PendingRuleReservations.Remove(ev.RuleId);

            UpdateReservedPoints(budget);
        }
    }

    private void OnGameRuleEnded(ref GameRuleEndedEvent ev)
    {
        if (!TryGetActiveBudget(out var budget))
            return;

        if (budget.Reservations.Remove(ev.RuleEntity))
        {
            UpdateReservedPoints(budget);
            return;
        }

        if (!budget.PendingRuleReservations.TryGetValue(ev.RuleId, out var pending) || pending.Count == 0)
            return;

        pending.Dequeue();
        if (pending.Count == 0)
            budget.PendingRuleReservations.Remove(ev.RuleId);

        UpdateReservedPoints(budget);
    }

    private void OnBudgetStartup(Entity<SecretTPComponent> ent, ref ComponentStartup args)
    {
        RecalculateTotal(ent.Comp);
    }

    private void OnBudgetShutdown(Entity<SecretTPComponent> ent, ref ComponentShutdown args)
    {
        _pendingDeaths.Clear();
    }

    private void OnBudgetRemove(Entity<SecretTPComponent> ent, ref ComponentRemove args)
    {
        _pendingDeaths.Clear();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!TryGetActiveBudget(out _))
            return;

        if (!HasComp<MindContainerComponent>(args.Target))
            return;

        if (args.NewMobState == MobState.Dead)
            _pendingDeaths[args.Target] = _timing.CurTime + TimeSpan.FromSeconds(GetReleaseSeconds());
        else if (args.NewMobState is MobState.Alive or MobState.Critical)
            _pendingDeaths.Remove(args.Target);
    }

    private void OnMindRemoved(MindRemovedMessage args)
    {
        if (!TryGetActiveBudget(out _))
            return;

        _pendingDeaths.Remove(args.Container.Owner);
    }

    private void OnRuleFilter(PirateSecretPlusRuleFilterEvent ev)
    {
        if (!TryComp<SecretTPComponent>(ev.Scheduler, out var budget))
            return;

        if (budget.RuleBlacklist.Contains(ev.Rule))
        {
            ev.Cancelled = true;
            return;
        }

        if (GetMissingDepartmentRequirements(budget, ev.Rule).Count > 0)
        {
            ev.Cancelled = true;
            return;
        }

        var cost = EstimateRuleCost(budget, ev.Prototype, ev.PlayerCount);
        if (cost > GetAvailablePoints(budget))
            ev.Cancelled = true;
    }

    private void OnRuleStarted(PirateSecretPlusRuleStartedEvent ev)
    {
        if (!TryComp<SecretTPComponent>(ev.Scheduler, out var budget))
            return;

        var cost = 0;
        if (TryComp<AntagSelectionComponent>(ev.RuleEntity, out var selection))
        {
            if (_ticker.RunLevel != GameRunLevel.InRound)
                selection.SelectionTime = AntagSelectionTime.PostPlayerSpawn;

            foreach (var definition in selection.Definitions)
            {
                var count = _antagSelection.GetTargetAntagCount(
                    (ev.RuleEntity, selection),
                    ev.PlayerCount,
                    definition);
                cost += count * GetDefinitionAntagCost(budget, definition);
            }
        }

        if (cost > 0)
            budget.Reservations[ev.RuleEntity] = cost;

        UpdateReservedPoints(budget);
    }

    public int GetAvailablePoints(SecretTPComponent budget)
    {
        RecalculateTotal(budget);
        ProcessExpiredReservations(budget);
        var used = GetUsedPoints(budget);
        return Math.Max(0, budget.TotalPoints - used - budget.Reservations.Values.Sum() -
            budget.PendingRuleReservations.Values.SelectMany(x => x).Sum());
    }

    public int GetUsedPoints(SecretTPComponent budget)
    {
        var expiredDeaths = _pendingDeaths
            .Where(pair => !Exists(pair.Key) || _timing.CurTime >= pair.Value)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var body in expiredDeaths)
            _pendingDeaths.Remove(body);

        var used = 0;
        var query = EntityQueryEnumerator<MindContainerComponent>();
        while (query.MoveNext(out var entity, out var container))
        {
            if (!container.HasMind || HasComp<CryostorageContainedComponent>(entity) ||
                container.Mind is not { } mindId ||
                !TryComp<MindComponent>(mindId, out var mind))
                continue;

            if (!TryComp<MobStateComponent>(entity, out var mob))
                continue;

            if (_mobState.IsDead(entity, mob))
            {
                if (!IsDeathGraceActive(entity, mind))
                    continue;
            }

            var countedAntags = new HashSet<string>();
            foreach (var roleEnt in mind.MindRoleContainer.ContainedEntities)
            {
                if (!TryComp<MindRoleComponent>(roleEnt, out var role) ||
                    role.AntagPrototype is not { } antag)
                    continue;

                if (countedAntags.Add(antag.Id) && budget.AntagPoints.TryGetValue(new ProtoId<AntagPrototype>(antag.Id), out var points))
                    used += points;
            }
        }

        return used;
    }

    private bool IsDeathGraceActive(EntityUid body, MindComponent mind)
    {
        if (_pendingDeaths.TryGetValue(body, out var releaseAt))
            return _timing.CurTime < releaseAt;

        return mind.TimeOfDeath is { } timeOfDeath &&
            _timing.RealTime - timeOfDeath < TimeSpan.FromSeconds(GetReleaseSeconds());
    }

    private void RecalculateTotal(SecretTPComponent budget)
    {
        budget.TotalPoints = 0;

        var hasAssignedCrew = false;
        var liveQuery = EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        while (liveQuery.MoveNext(out var entity, out var container, out var mob))
        {
            if (!container.HasMind || !_jobs.MindTryGetJobId(container.Mind, out var job) ||
                job is not { } jobId)
                continue;

            hasAssignedCrew = true;
            if (_mobState.IsAlive(entity, mob) && budget.JobPoints.TryGetValue(new ProtoId<JobPrototype>(jobId.Id), out var points))
                budget.TotalPoints += points;
        }

        if (hasAssignedCrew)
            return;

        foreach (var (job, count) in GetSecretTPReadyManifest())
        {
            if (budget.JobPoints.TryGetValue(job, out var points))
                budget.TotalPoints += points * count;
        }
    }

    private Dictionary<ProtoId<JobPrototype>, int> GetSecretTPReadyManifest()
    {
        var result = new Dictionary<ProtoId<JobPrototype>, int>();

        foreach (var (userId, status) in _ticker.PlayerGameStatuses)
        {
            if (status != PlayerGameStatus.ReadyToPlay ||
                !_prefsManager.TryGetCachedPreferences(userId, out var preferences) ||
                preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            foreach (var (jobId, priority) in profile.JobPriorities)
            {
                var job = new ProtoId<JobPrototype>(jobId);
                if (priority != JobPriority.High &&
                    (!_prototypes.TryIndex(job, out var prototype) ||
                     prototype.Weight < 10 || priority <= JobPriority.Never))
                    continue;

                result[job] = result.GetValueOrDefault(job) + 1;
            }
        }

        return result;
    }

    private int EstimateRuleCost(SecretTPComponent budget, EntityPrototype proto, int players)
    {
        if (!proto.TryGetComponent<AntagSelectionComponent>(out var selection, _factory))
            return 0;

        var cost = 0;
        foreach (var definition in selection.Definitions)
        {
            var targetCount = players > 0
                ? _antagSelection.GetTargetAntagCount((EntityUid.Invalid, selection), players, definition)
                : definition.Min;
            cost += targetCount * GetDefinitionAntagCost(budget, definition);
        }

        return cost;
    }

    private int GetDefinitionAntagCost(SecretTPComponent budget, AntagSelectionDefinition definition)
    {
        var role = definition.PrefRoles.FirstOrDefault();
        if (string.IsNullOrEmpty(role.Id))
            role = definition.FallbackRoles.FirstOrDefault();

        if (!string.IsNullOrEmpty(role.Id) && budget.AntagPoints.TryGetValue(new ProtoId<AntagPrototype>(role.Id), out var roleCost))
            return roleCost;

        if (definition.MindRoles is null)
            return 0;

        foreach (var mindRoleId in definition.MindRoles)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(mindRoleId, out var mindRolePrototype) ||
                !mindRolePrototype.Components.TryGetValue(_factory.GetComponentName<MindRoleComponent>(), out var component))
                continue;

            var mindRole = (MindRoleComponent) component.Component;
            if (mindRole.AntagPrototype is { } antag && budget.AntagPoints.TryGetValue(new ProtoId<AntagPrototype>(antag.Id), out var mindRoleCost))
                return mindRoleCost;
        }

        return 0;
    }

    private List<(ProtoId<DepartmentPrototype> Department, int Required, int Available)> GetMissingDepartmentRequirements(
        SecretTPComponent budget,
        ProtoId<EntityPrototype> rule)
    {
        if (!budget.RuleMinimumAliveDepartments.TryGetValue(rule, out var requirements) ||
            requirements is null || requirements.Count == 0)
            return [];

        var available = new Dictionary<ProtoId<DepartmentPrototype>, int>();

        var hasAssignedCrew = false;
        var query = EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        while (query.MoveNext(out var entity, out var container, out var mob))
        {
            if (!container.HasMind || !_jobs.MindTryGetJobId(container.Mind, out var job) ||
                job is not { } jobIdValue)
                continue;

            hasAssignedCrew = true;
            if (!_mobState.IsAlive(entity, mob) ||
                !_jobs.TryGetAllDepartments(jobIdValue.Id, out var departments))
                continue;

            foreach (var department in departments)
                available[department.ID] = available.GetValueOrDefault(department.ID) + 1;
        }

        if (!hasAssignedCrew)
        {
            var ready = GetSecretTPReadyManifest();
            foreach (var (job, jobCount) in ready)
            {
                if (!_jobs.TryGetAllDepartments(job.Id, out var departments))
                    continue;

                foreach (var department in departments)
                    available[department.ID] = available.GetValueOrDefault(department.ID) + jobCount;
            }
        }

        var missing = new List<(ProtoId<DepartmentPrototype> Department, int Required, int Available)>();
        foreach (var (department, required) in requirements)
        {
            var count = available.GetValueOrDefault(department);
            if (count < required)
                missing.Add((department, required, count));
        }

        return missing;
    }

    private string GetDepartmentName(ProtoId<DepartmentPrototype> department)
    {
        return _prototypes.TryIndex(department, out DepartmentPrototype? prototype)
            ? Loc.GetString(prototype.Name)
            : department.Id;
    }

    private string FormatMissingDepartmentRequirementsLocalized(
        string rule,
        List<(ProtoId<DepartmentPrototype> Department, int Required, int Available)> missing)
    {
        var departments = string.Join("; ", missing.Select(requirement =>
            Loc.GetString("cmd-secrettp-department-entry",
                ("department", GetDepartmentName(requirement.Department)),
                ("required", requirement.Required),
                ("available", requirement.Available))));

        return Loc.GetString("cmd-secrettp-rule-missing-departments",
            ("rule", rule),
            ("departments", departments));
    }

    private void ProcessExpiredReservations(SecretTPComponent budget)
    {
        var expired = new List<EntityUid>();
        foreach (var (rule, _) in budget.Reservations)
        {
            if (!Exists(rule))
            {
                expired.Add(rule);
                continue;
            }

            if (!TryComp<AntagSelectionComponent>(rule, out var selection))
            {
                continue;
            }

            if (HasPendingGhostRole(rule))
                continue;

            if (selection.AssignedMinds.Count == 0 &&
                (!selection.AssignmentComplete || HasLateJoinAdditional(selection)))
                continue;

            expired.Add(rule);
        }

        foreach (var rule in expired)
            budget.Reservations.Remove(rule);

        UpdateReservedPoints(budget);
    }

    private static void UpdateReservedPoints(SecretTPComponent budget)
    {
        budget.ReservedPoints = budget.Reservations.Values.Sum() +
            budget.PendingRuleReservations.Values.SelectMany(x => x).Sum();
    }

    private bool HasPendingGhostRole(EntityUid rule)
    {
        var query = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, GhostRoleComponent>();
        while (query.MoveNext(out _, out var spawner, out var ghostRole))
        {
            if (spawner.Rule == rule && !ghostRole.Taken)
                return true;
        }

        return false;
    }

    private static bool HasLateJoinAdditional(AntagSelectionComponent selection)
    {
        return selection.Definitions.Any(definition => definition.LateJoinAdditional);
    }

    private float GetReleaseSeconds()
    {
        return TryGetActiveBudget(out var budget) ? budget.DeathReleaseSeconds : 900f;
    }

    public bool TryGetActiveBudget(out SecretTPComponent budget)
    {
        var query = EntityQueryEnumerator<SecretTPComponent, ActiveGameRuleComponent>();
        if (query.MoveNext(out _, out var foundBudget, out _))
        {
            budget = foundBudget!;
            return true;
        }

        budget = default!;
        return false;
    }
}
