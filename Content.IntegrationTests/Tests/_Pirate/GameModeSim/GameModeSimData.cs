#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Goobstation.Shared.StationEvents;
using Content.Pirate.Server.SecretTP.Components;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Presets;
using Content.Server.Psionics.Glimmer;
using Content.Server.StationEvents.Components;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.GameModeSim;

public sealed class GameModeSimData
{
    public readonly Dictionary<string, SimRule> Rules = new();
    public readonly List<SimRule> StationEvents = new();
    public readonly Dictionary<string, EntityTableSelector> Tables = new();
    public readonly Dictionary<string, Dictionary<string, float>> WeightTables = new();
    public readonly Dictionary<string, HashSet<string>> IncompatibleModes = new();
    public readonly Dictionary<string, List<string>> JobDepartments = new();
    public readonly Dictionary<string, SimPreset> Presets = new();

    public static GameModeSimData Extract(IPrototypeManager protos, IComponentFactory factory, IEnumerable<string> presetIds)
    {
        var data = new GameModeSimData();

        foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryComp<GameRuleComponent>(out var gameRule, factory))
                continue;

            var rule = ExtractRule(proto, gameRule, protos, factory);
            data.Rules[rule.Id] = rule;
            if (rule.Event != null)
                data.StationEvents.Add(rule);
        }

        foreach (var table in protos.EnumeratePrototypes<EntityTablePrototype>())
            data.Tables[table.ID] = table.Table;

        foreach (var weights in protos.EnumeratePrototypes<WeightedRandomPrototype>())
            data.WeightTables[weights.ID] = new Dictionary<string, float>(weights.Weights);

        foreach (var incompatible in protos.EnumeratePrototypes<IncompatibleGameModesPrototype>())
            data.IncompatibleModes[incompatible.ID] = new HashSet<string>(incompatible.Modes);

        foreach (var department in protos.EnumeratePrototypes<DepartmentPrototype>())
        {
            foreach (var job in department.Roles)
            {
                if (!data.JobDepartments.TryGetValue(job.Id, out var list))
                    data.JobDepartments[job.Id] = list = new List<string>();
                list.Add(department.ID);
            }
        }

        foreach (var presetId in presetIds)
        {
            var preset = FindPreset(protos, presetId)
                ?? throw new ArgumentException($"Unknown game preset '{presetId}'");
            data.Presets[presetId] = ExtractPreset(preset, protos, factory);
        }

        return data;
    }

    // Ghost-role rules without AntagSelection would otherwise be missing from antag counts.
    public void AddGhostRoleSpawns(Dictionary<string, int> rules)
    {
        foreach (var (id, count) in rules)
        {
            if (!Rules.TryGetValue(id, out var rule) || rule.Antags != null)
                continue;

            rule.Antags =
            [
                new SimAntagDef
                {
                    Min = count,
                    Max = count,
                    PlayerRatio = 1,
                    PickPlayer = false,
                    HasSpawner = true,
                    Label = id.EndsWith("Spawn", StringComparison.Ordinal) ? id[..^"Spawn".Length] : id,
                },
            ];
        }
    }

    private static GamePresetPrototype? FindPreset(IPrototypeManager protos, string id)
    {
        if (protos.TryIndex<GamePresetPrototype>(id, out var preset))
            return preset;

        return protos.EnumeratePrototypes<GamePresetPrototype>()
            .FirstOrDefault(p => p.Alias.Any(a => string.Equals(a, id, StringComparison.OrdinalIgnoreCase)));
    }

    private static SimPreset ExtractPreset(GamePresetPrototype preset, IPrototypeManager protos, IComponentFactory factory)
    {
        var sim = new SimPreset { Id = preset.ID };
        foreach (var ruleId in preset.Rules)
        {
            var proto = protos.Index(ruleId);
            if (proto.TryComp<SecretPlusComponent>(out var plus, factory))
            {
                sim.Chaos.Add(ChaosFrom(proto, plus.MinStartingChaos, plus.MaxStartingChaos, plus.LivingChaosChange,
                    plus.DeadChaosChange, plus.ChaosChangeVariationMin, plus.ChaosChangeVariationMax,
                    plus.ChaosChangeVariationExponent, plus.ChaosOffset, plus.ChaosExponent, plus.ChaosMatching,
                    plus.ChaosThreshold, plus.SpeedRamping, plus.NoRoundstartAntags, plus.IgnoreTimings,
                    plus.IgnoreIncompatible, plus.DisallowedEvents.Select(e => e.Id), plus.EventIntervalMin,
                    plus.EventIntervalMax, plus.PrimaryAntagsWeightTable, plus.RoundStartAntagsWeightTable,
                    plus.PrimaryAntagChaosBias, salt: false, factory));
            }
            else if (proto.TryComp<SecretSaltPlusComponent>(out var salt, factory))
            {
                sim.Chaos.Add(ChaosFrom(proto, salt.MinStartingChaos, salt.MaxStartingChaos, salt.LivingChaosChange,
                    salt.DeadChaosChange, salt.ChaosChangeVariationMin, salt.ChaosChangeVariationMax,
                    salt.ChaosChangeVariationExponent, salt.ChaosOffset, salt.ChaosExponent, salt.ChaosMatching,
                    salt.ChaosThreshold, salt.SpeedRamping, salt.NoRoundstartAntags, salt.IgnoreTimings,
                    salt.IgnoreIncompatible, salt.DisallowedEvents.Select(e => e.Id), salt.EventIntervalMin,
                    salt.EventIntervalMax, salt.PrimaryAntagsWeightTable, salt.RoundStartAntagsWeightTable,
                    salt.PrimaryAntagChaosBias, salt: true, factory));
            }
            else if (proto.TryComp<BasicStationEventSchedulerComponent>(out var basic, factory))
            {
                sim.Basic.Add(new SimBasicScheduler
                {
                    Id = proto.ID,
                    MinimumTimeUntilFirstEvent = basic.MinimumTimeUntilFirstEvent,
                    MinTiming = basic.MinMaxEventTiming.Min,
                    MaxTiming = basic.MinMaxEventTiming.Max,
                    Table = basic.ScheduledGameRules,
                });
            }
            else
            {
                sim.Unsimulated.Add(proto.ID);
            }
        }

        return sim;
    }

    private static SimChaosScheduler ChaosFrom(EntityPrototype proto, float minStart, float maxStart, float living,
        float dead, float varMin, float varMax, float varExp, float offset, float exponent, float matching,
        float threshold, float ramping, bool noRoundstart, bool ignoreTimings, bool ignoreIncompatible,
        IEnumerable<string> disallowed, TimeSpan intervalMin, TimeSpan intervalMax, string primary, string roundstart,
        float primaryBias, bool salt, IComponentFactory factory)
    {
        var sim = new SimChaosScheduler
        {
            Id = proto.ID,
            Salt = salt,
            MinStartingChaos = minStart,
            MaxStartingChaos = maxStart,
            LivingChaosChange = living,
            DeadChaosChange = dead,
            ChaosChangeVariationMin = varMin,
            ChaosChangeVariationMax = varMax,
            ChaosChangeVariationExponent = varExp,
            ChaosOffset = offset,
            ChaosExponent = exponent,
            ChaosMatching = matching,
            ChaosThreshold = threshold,
            SpeedRamping = ramping,
            NoRoundstartAntags = noRoundstart,
            IgnoreTimings = ignoreTimings,
            IgnoreIncompatible = ignoreIncompatible,
            DisallowedEvents = disallowed.ToHashSet(),
            EventIntervalMin = intervalMin.TotalSeconds,
            EventIntervalMax = intervalMax.TotalSeconds,
            PrimaryTable = primary,
            RoundstartTable = roundstart,
            PrimaryAntagChaosBias = primaryBias,
            // SecretPlusSystem raises SecretTP hooks only for this exact prototype id.
            TpHooks = !salt && proto.ID == Content.Shared.GameTicking.SecretTPConstants.RuleId,
        };

        if (proto.TryComp<SelectedGameRulesComponent>(out var selected, factory))
            sim.Table = selected.ScheduledGameRules;

        if (proto.TryComp<SecretTPComponent>(out var tp, factory))
        {
            sim.Tp = new SimTpBudget
            {
                JobPoints = tp.JobPoints.ToDictionary(p => p.Key.Id, p => p.Value),
                AntagPoints = tp.AntagPoints.ToDictionary(p => p.Key.Id, p => p.Value),
                RuleBlacklist = tp.RuleBlacklist.Select(r => r.Id).ToHashSet(),
                RuleMinimumAliveDepartments = tp.RuleMinimumAliveDepartments.ToDictionary(
                    p => p.Key.Id,
                    p => p.Value?.ToDictionary(d => d.Key.Id, d => d.Value) ?? new Dictionary<string, int>()),
                GreenShiftWeight = tp.GreenShiftWeight,
                RedShiftWeight = tp.RedShiftWeight,
                DeathReleaseSeconds = tp.DeathReleaseSeconds,
            };
        }

        return sim;
    }

    private static SimRule ExtractRule(EntityPrototype proto, GameRuleComponent gameRule, IPrototypeManager protos, IComponentFactory factory)
    {
        var rule = new SimRule
        {
            Id = proto.ID,
            RuleChaosScore = gameRule.ChaosScore,
            MinPlayers = gameRule.MinPlayers,
            DelayMin = gameRule.Delay?.Min,
            DelayMax = gameRule.Delay?.Max,
        };

        if (proto.TryComp<TagComponent>(out var tags, factory))
            rule.LoneRun = tags.Tags.Any(t => t.Id == "LoneRunRule");

        if (proto.TryComp<StationEventComponent>(out var ev, factory))
        {
            rule.Event = new SimStationEvent
            {
                Weight = ev.Weight,
                EarliestStart = ev.EarliestStart,
                ReoccurrenceDelay = ev.ReoccurrenceDelay,
                Duration = ev.Duration?.TotalSeconds,
                MaxDuration = ev.MaxDuration?.TotalSeconds,
                MinimumPlayers = ev.MinimumPlayers,
                MaxOccurrences = ev.MaxOccurrences,
                OccursDuringRoundEnd = ev.OccursDuringRoundEnd,
                EventType = ev.EventType.Id,
            };
        }

        if (proto.TryComp<GlimmerEventComponent>(out var glimmer, factory))
        {
            rule.Glimmer = new SimGlimmerBand
            {
                Minimum = glimmer.MinimumGlimmer,
                Maximum = glimmer.MaximumGlimmer,
                BurnLower = glimmer.GlimmerBurnLower,
                BurnUpper = glimmer.GlimmerBurnUpper,
            };
        }

        if (proto.TryComp<AntagSelectionComponent>(out var selection, factory))
        {
            rule.Antags = new List<SimAntagDef>();
            foreach (var def in selection.Definitions)
            {
                var first = def.PrefRoles.FirstOrDefault().Id;
                if (string.IsNullOrEmpty(first))
                    first = def.FallbackRoles.FirstOrDefault().Id;

                var mindRoleAntags = new List<string>();
                foreach (var mindRole in def.MindRoles ?? new())
                {
                    if (protos.TryIndex<EntityPrototype>(mindRole, out var mindProto)
                        && mindProto.TryComp<MindRoleComponent>(out var role, factory)
                        && role.AntagPrototype is { } antag)
                    {
                        mindRoleAntags.Add(antag.Id);
                    }
                }

                rule.Antags.Add(new SimAntagDef
                {
                    Min = def.Min,
                    Max = def.Max,
                    MinRange = def.MinRange is { } minRange ? (minRange.Min, minRange.Max) : null,
                    MaxRange = def.MaxRange is { } maxRange ? (maxRange.Min, maxRange.Max) : null,
                    PlayerRatio = def.PlayerRatio,
                    ChaosScore = def.ChaosScore,
                    PickPlayer = def.PickPlayer,
                    HasSpawner = def.SpawnerPrototype != null,
                    FirstRole = string.IsNullOrEmpty(first) ? null : first,
                    MindRoleAntags = mindRoleAntags,
                    Label = !string.IsNullOrEmpty(first) ? first : mindRoleAntags.FirstOrDefault() ?? proto.ID,
                });
            }
        }

        return rule;
    }
}

public sealed class SimPreset
{
    public string Id = "";
    public readonly List<SimChaosScheduler> Chaos = new();
    public readonly List<SimBasicScheduler> Basic = new();
    public readonly List<string> Unsimulated = new();
}

public sealed class SimRule
{
    public string Id = "";
    public float? RuleChaosScore;
    public int MinPlayers;
    public int? DelayMin;
    public int? DelayMax;
    public bool LoneRun;
    public SimStationEvent? Event;
    public SimGlimmerBand? Glimmer;
    public List<SimAntagDef>? Antags;
}

public sealed class SimStationEvent
{
    public float Weight;
    public int EarliestStart;
    public int ReoccurrenceDelay;
    public double? Duration;
    public double? MaxDuration;
    public int MinimumPlayers;
    public int? MaxOccurrences;
    public bool OccursDuringRoundEnd;
    public string EventType = "";
}

public sealed class SimGlimmerBand
{
    public int Minimum;
    public int Maximum;
    public int BurnLower;
    public int BurnUpper;
}

public sealed class SimAntagDef
{
    public int Min;
    public int Max;
    public (int Min, int Max)? MinRange;
    public (int Min, int Max)? MaxRange;
    public int PlayerRatio;
    public float? ChaosScore;
    public bool PickPlayer;
    public bool HasSpawner;
    public string? FirstRole;
    public List<string> MindRoleAntags = new();
    public string Label = "";
}

public sealed class SimChaosScheduler
{
    public string Id = "";
    public bool Salt;
    public bool TpHooks;
    public SimTpBudget? Tp;
    public EntityTableSelector? Table;
    public float MinStartingChaos;
    public float MaxStartingChaos;
    public float LivingChaosChange;
    public float DeadChaosChange;
    public float ChaosChangeVariationMin;
    public float ChaosChangeVariationMax;
    public float ChaosChangeVariationExponent;
    public float ChaosOffset;
    public float ChaosExponent;
    public float ChaosMatching;
    public float ChaosThreshold;
    public float SpeedRamping;
    public bool NoRoundstartAntags;
    public bool IgnoreTimings;
    public bool IgnoreIncompatible;
    public HashSet<string> DisallowedEvents = new();
    public double EventIntervalMin;
    public double EventIntervalMax;
    public string PrimaryTable = "";
    public string RoundstartTable = "";
    public float PrimaryAntagChaosBias;
}

public sealed class SimTpBudget
{
    public Dictionary<string, int> JobPoints = new();
    public Dictionary<string, int> AntagPoints = new();
    public HashSet<string> RuleBlacklist = new();
    public Dictionary<string, Dictionary<string, int>> RuleMinimumAliveDepartments = new();
    public float GreenShiftWeight;
    public float RedShiftWeight;
    public float DeathReleaseSeconds;
}

public sealed class SimBasicScheduler
{
    public string Id = "";
    public float MinimumTimeUntilFirstEvent;
    public int MinTiming;
    public int MaxTiming;
    public EntityTableSelector Table = default!;
}
