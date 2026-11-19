#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Server.StationEvents.Metric;

namespace Content.Pirate.GameModeSim;

// Pirate: mirrors GameDirector's threat purchases and response policy against the simulated population.
public sealed partial class GameModeSimulator
{
    private readonly record struct DirectorThreat(float Live, float Pending, float Target);
    private readonly record struct DirectorAntagPick(string Id, int Count, float Threat, bool GhostRole, float Score);
    private readonly record struct DirectorAntagRange(int PreferredMin, int PreferredMax, int HardMin, int HardMax, bool GhostRole);

    private void PlanDirectorRoundstart(DirectorState state, StoryPrototype story, int players)
    {
        var d = state.Scheduler;
        var scale = story.RoundstartThreat * d.RoundstartThreatScale;
        if (scale <= 0 || story.Roundstart.Count == 0)
            return;

        var budget = MathF.Max(d.ThreatPerPlayer * story.Threat * scale * players, d.MinRoundstartThreat);
        var remaining = budget;
        var chosen = new List<string>();
        foreach (var slot in story.Roundstart)
        {
            if (_rand.NextDouble() >= slot.Prob)
                continue;

            var slotBudget = slot.Share is { } share ? MathF.Min(budget * share, remaining) : remaining;
            var picks = new List<DirectorAntagPick>();
            foreach (var entry in _data.AntagPools[slot.Pool.Id].Rules)
            {
                var id = entry.Id.Id;
                if (chosen.Contains(id) || !_data.Rules.TryGetValue(id, out var rule)
                    || !DirectorCompatibleWith(id, chosen) || rule.MinPlayers > players)
                    continue;
                if (EvaluateDirectorAntag(d, rule, entry, slotBudget, int.MaxValue, false) is { } pick)
                    picks.Add(pick);
            }
            if (picks.Count == 0)
                continue;

            var picked = DirectorPick(picks, p => p.Score);
            StartDirectorPurchase(state, picked, true);
            chosen.Add(picked.Id);
            remaining -= picked.Count * picked.Threat;
        }
    }

    private bool DirectorCompatibleWith(string rule, List<string> chosen)
    {
        foreach (var other in chosen)
        {
            if (_data.IncompatibleModes.TryGetValue(rule, out var incompatible) && incompatible.Contains(other)
                || _data.IncompatibleModes.TryGetValue(other, out var reverse) && reverse.Contains(rule))
                return false;
        }
        return true;
    }

    private static float DirectorRuleThreat(AntagThreatPrototype table, string rule)
    {
        return table.Rules.TryGetValue(rule, out var threat) ? threat : table.Default;
    }

    private DirectorThreat MeasureDirectorThreat(DirectorState state, StoryBeatPrototype beat, DirectorPlayerCount count)
    {
        var d = state.Scheduler;
        var table = _data.ThreatTables[d.ThreatTable];
        var live = 0f;
        foreach (var person in _people)
        {
            if (!person.Present || person.InLobby || person.Observer || !person.Alive || person.Antag is not { } antag)
                continue;

            // Synthetic ghost roles have no AntagSelection.AssignedMinds in the game.
            var hasSelection = _data.Rules[antag.RuleId].Antags?.Any(def => !def.Synthetic) == true;
            live += hasSelection ? DirectorRuleThreat(table, antag.RuleId) : table.Convert;
        }

        var pending = state.Purchases.Sum(p => MissingDirectorAntags(state, p) * p.Threat);
        var target = 0f;
        if (beat.Threat > 0 && !beat.HelpOnly && state.CurrentStory is { } story)
        {
            target = d.ThreatPerPlayer * _data.Stories[story].Threat * beat.Threat * count.Players;
            target = MathF.Min(target, d.BaseThreatCap + d.ThreatCapPerSecurity * count.Staff("Security"));
        }
        return new DirectorThreat(live, pending, target);
    }

    private int MissingDirectorAntags(DirectorState state, DirectorPurchase purchase)
    {
        var d = state.Scheduler;
        var window = purchase.Roundstart ? d.RoundstartPendingTime : d.PendingThreatTime;
        if (purchase.Rule.Started && (_now - purchase.Time) * _cfg.EventSpeedup > window)
            return 0;

        var assigned = purchase.Rule.Rule.Antags?.Any(def => !def.Synthetic) == true ? purchase.Rule.AntagSpawned : 0;
        return Math.Max(0, purchase.Count - assigned);
    }

    private bool TryPurchaseDirectorThreat(DirectorState state, DirectorPlayerCount count, DirectorThreat threat)
    {
        var d = state.Scheduler;
        var gap = threat.Target - threat.Live - threat.Pending;
        if (gap < d.MinThreatPurchase || state.CurrentStory is not { } id || _data.Stories[id].Midround.Count == 0)
            return false;

        var ghosts = count.Ghosts - state.Purchases.Where(p => p.GhostRole).Sum(p => MissingDirectorAntags(state, p));
        var seen = new HashSet<string>();
        var picks = new List<DirectorAntagPick>();
        foreach (var entry in _data.Stories[id].Midround.SelectMany(pool => _data.AntagPools[pool.Id].Rules))
        {
            if (!seen.Add(entry.Id.Id) || !_data.Rules.TryGetValue(entry.Id.Id, out var rule)
                || rule.MinPlayers > count.InGame || rule.Event != null && !CanRun(rule, count.InGame, _now, 1f))
                continue;

            if (EvaluateDirectorAntag(d, rule, entry, gap, ghosts, true) is { } pick)
                picks.Add(pick with { Score = pick.Score * RepeatMultiplier(state, pick.Id) });
        }
        if (picks.Count == 0)
            return false;

        var chosen = DirectorPick(picks, p => p.Score);
        var instance = StartDirectorPurchase(state, chosen, false);
        if (instance.Rule.Event == null)
            StartRuleInstance(instance);
        state.Fired.Add(new FiredDirectorEvent(instance, _now, new ChaosMetrics()));
        state.LastSpotlight["Security"] = _now;
        return true;
    }

    private DirectorAntagPick? EvaluateDirectorAntag(SimDirector d, SimRule rule, AntagPoolEntry entry,
        float budget, int ghosts, bool midround)
    {
        var table = _data.ThreatTables[d.ThreatTable];
        var threat = DirectorRuleThreat(table, rule.Id);
        if (threat <= 0 || budget <= 0)
            return null;

        var range = DirectorCountRange(d, rule, entry, midround);
        var hardMax = range.GhostRole ? Math.Min(range.HardMax, ghosts) : range.HardMax;
        if (hardMax < range.HardMin)
            return null;

        var preferredMax = Math.Min(range.PreferredMax, hardMax);
        var wanted = (int) MathF.Round(budget / threat);
        var best = 0;
        var bestScore = 0f;
        foreach (var option in new[] { Math.Clamp(wanted, range.PreferredMin, Math.Max(range.PreferredMin, preferredMax)), Math.Clamp(wanted, range.HardMin, hardMax) })
        {
            if (option > hardMax)
                continue;
            var inRange = option >= range.PreferredMin && option <= range.PreferredMax;
            var miss = (option * threat - budget) / budget;
            var fit = miss <= 0 ? 1f + 0.5f * miss : MathF.Max(0f, 1f - 1.5f * miss);
            var score = fit * (inRange ? 1f : d.OutOfRangePenalty);
            if (score <= bestScore)
                continue;
            best = option;
            bestScore = score;
        }
        if (best == 0 || bestScore < 0.25f)
            return null;

        var weight = entry.Weight ?? rule.Event?.Weight ?? 1f;
        return new DirectorAntagPick(rule.Id, best, threat, range.GhostRole, weight * bestScore);
    }

    private static DirectorAntagRange DirectorCountRange(SimDirector d, SimRule rule, AntagPoolEntry entry, bool midround)
    {
        var defs = rule.Antags;
        if (defs == null || defs.Count == 0 || defs.All(def => def.Synthetic))
            return new DirectorAntagRange(1, 1, 1, 1, false);

        var min = 0;
        var max = 0;
        var ghostRole = midround;
        foreach (var def in defs)
        {
            var defMin = def.MinRange?.Min ?? def.Min;
            min += defMin;
            max += Math.Max(defMin, def.MaxRange?.Max ?? def.Max);
            ghostRole &= def.HasSpawner && (!def.PickPlayer || rule.SelectionPrePlayerSpawn);
        }
        var hardMin = Math.Max(1, entry.Min ?? min);
        var hardMax = Math.Max(hardMin, entry.Max ?? (int) MathF.Ceiling(Math.Max(1, max) * d.CountStretch));
        var preferredMin = Math.Clamp(min, hardMin, hardMax);
        var preferredMax = Math.Clamp(max, preferredMin, hardMax);
        return new DirectorAntagRange(preferredMin, preferredMax, hardMin, hardMax, ghostRole);
    }

    private RuleInstance StartDirectorPurchase(DirectorState state, DirectorAntagPick pick, bool roundstart)
    {
        var instance = AddRule(pick.Id, roundstart ? "roundstart" : "midround");
        ApplyDirectorAntagCount(instance, pick.Count);
        state.Purchases.Add(new DirectorPurchase
        {
            Rule = instance,
            Threat = pick.Threat,
            Count = pick.Count,
            Time = _now,
            Roundstart = roundstart,
            GhostRole = pick.GhostRole,
        });
        return instance;
    }

    private static void ApplyDirectorAntagCount(RuleInstance instance, int count)
    {
        var defs = instance.Rule.Antags;
        if (defs == null || defs.Count == 0 || defs.All(def => def.Synthetic))
            return;

        var mins = defs.Select(d => d.MinRange?.Min ?? d.Min).ToArray();
        var maxes = defs.Select((d, i) => Math.Max(mins[i], d.MaxRange?.Max ?? d.Max)).ToArray();
        var scalable = Enumerable.Range(0, defs.Count).Where(i => maxes[i] > mins[i]).ToList();
        var baseTotal = mins.Sum();
        var counts = new int[defs.Count];
        if (scalable.Count == 0 || count < baseTotal)
        {
            var total = Math.Max(1, baseTotal);
            var assigned = 0;
            for (var i = 0; i < defs.Count; i++)
            {
                counts[i] = mins[i] * count / total;
                assigned += counts[i];
            }
            var order = Enumerable.Range(0, defs.Count).OrderByDescending(i => mins[i]).ToList();
            for (var i = 0; assigned < count; i++, assigned++)
                counts[order[i % order.Count]]++;
        }
        else
        {
            Array.Copy(mins, counts, defs.Count);
            var remaining = count - baseTotal;
            while (remaining > 0 && scalable.Any(i => counts[i] < maxes[i]))
            {
                foreach (var i in scalable)
                {
                    if (remaining == 0 || counts[i] >= maxes[i])
                        continue;
                    counts[i]++;
                    remaining--;
                }
            }
            for (var i = 0; remaining > 0; i++, remaining--)
                counts[scalable[i % scalable.Count]]++;
        }
        for (var i = 0; i < defs.Count; i++)
            instance.Defs[i] = instance.Defs[i] with { Min = counts[i], Max = counts[i] };
    }

    private void ScheduleDirectorEvent(DirectorState state, StoryBeatPrototype beat, DirectorPlayerCount count)
    {
        var d = state.Scheduler;
        var scale = count.Players >= d.LowPopPlayers
            ? 1f
            : MathF.Min(d.MaxLowPopDelayScale, MathF.Sqrt((float) d.LowPopPlayers / Math.Max(1, count.Players)));
        state.TimeNextEvent = _now + NextFloat(beat.EventDelayMin, beat.EventDelayMax) * scale / _cfg.EventSpeedup;
    }

    private static bool IsDirectorHelpEvent(DirectorEvent possible)
    {
        return !possible.Chaos.Empty && possible.Chaos.ChaosDict.Values.All(v => v <= 0);
    }

    private bool UpdateDirectorRelax(DirectorState state, ChaosMetrics chaos, DirectorPlayerCount count)
    {
        var d = state.Scheduler;
        var scale = PopulationScale(state, count);
        var worst = 0f;
        foreach (var metric in d.RelaxMetrics)
            worst = MathF.Max(worst, (float) chaos.ChaosDict.GetValueOrDefault(metric) / DirectorMetricScale(d, metric, scale));
        if (worst > 0 && worst >= d.RelaxStrain)
            state.RelaxUntil = _now + d.RelaxTime / _cfg.EventSpeedup;
        return _now < state.RelaxUntil;
    }

    private bool TryDirectorArmedResponse(DirectorState state, ChaosMetrics chaos, DirectorPlayerCount count)
    {
        var d = state.Scheduler;
        var unmatched = chaos.ChaosDict.GetValueOrDefault(ChaosMetric.Security);
        if (unmatched < d.ArmedResponseThreshold * PopulationScale(state, count))
        {
            state.OverwhelmedSince = 0;
            return false;
        }
        if (state.OverwhelmedSince == 0)
        {
            state.OverwhelmedSince = _now;
            return false;
        }
        if ((_now - state.OverwhelmedSince) * _cfg.EventSpeedup < d.ArmedResponseDelay
            || state.ArmedResponseCount >= d.MaxArmedResponses || d.ArmedResponses.Count == 0
            || count.Ghosts < d.ArmedResponseGhosts
            || state.LastArmedResponse != 0 && (_now - state.LastArmedResponse) * _cfg.EventSpeedup < d.ArmedResponseCooldown)
            return false;

        for (var i = Math.Min(state.ArmedResponseCount, d.ArmedResponses.Count - 1); i < d.ArmedResponses.Count; i++)
        {
            if (!_data.Rules.TryGetValue(d.ArmedResponses[i], out var rule)
                || rule.Event == null || !CanRun(rule, count.InGame, 0, 1f))
                continue;
            var instance = AddRule(rule.Id, "midround");
            state.Fired.Add(new FiredDirectorEvent(instance, _now, new ChaosMetrics()));
            state.ArmedResponseCount++;
            state.LastArmedResponse = _now;
            state.OverwhelmedSince = 0;
            return true;
        }
        return false;
    }

    private void CleanupDirectorSpawners(DirectorState state, bool all)
    {
        foreach (var purchase in state.Purchases)
        {
            if (purchase.Roundstart || !purchase.GhostRole || purchase.SpawnersCleared
                || !all && (_now - purchase.Time) * _cfg.EventSpeedup < state.Scheduler.SpawnerLifetime)
                continue;
            purchase.SpawnersCleared = true;
            _pendingGhostRoles.RemoveAll(role => role.Instance == purchase.Rule);
            SyncReservation(purchase.Rule);
        }
    }
}
