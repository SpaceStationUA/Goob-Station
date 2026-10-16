#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Content.IntegrationTests.Tests._Pirate.GameModeSim;

// Population and death rates are assumptions, not measurements.
public sealed class GameModeSimConfig
{
    public List<string> Presets { get; set; } = ["SecretPlusMid", "SecretPlusLow", "SecretSalt", "SecretTP", "Blueshift"];

    public List<GameModeSimScenario> Scenarios { get; set; } = [new()];

    public int Rounds { get; set; } = 1000;
    public int Seed { get; set; } = 1337;

    public string OutputDir { get; set; } = "Tools/_Pirate/GameModeSim/output";

    public double TimelineStepMinutes { get; set; } = 5;

    public float MinimumTimeUntilFirstEvent { get; set; } = 300f;
    public float RoundstartChaosScoreMultiplier { get; set; } = 1f;
    public float EventSpeedup { get; set; } = 1f;
    public int PlayerCountBias { get; set; } = 0;

    public int Observers { get; set; } = 2;

    public double EvacCallMinutesBeforeEnd { get; set; } = 10;

    public double CrewDeathPerMinute { get; set; } = 0.0025;

    public double CrewDeathPerAntagPerMinute { get; set; } = 0.0002;

    public double AntagDeathPerMinute { get; set; } = 0.003;

    public double AntagDeathPerSecPerMinute { get; set; } = 0.0006;

    public double RevivalChance { get; set; } = 0.35;
    public double RevivalMinMinutes { get; set; } = 3;
    public double RevivalMaxMinutes { get; set; } = 15;

    public double GhostRoleTakeChance { get; set; } = 0.85;

    public double GhostRoleTakeMedianMinutes { get; set; } = 3;

    public double AntagOptInChance { get; set; } = 0.85;

    // Ghost-role rules without AntagSelection would otherwise be missing from antag counts.
    public Dictionary<string, int> GhostRoleSpawnRules { get; set; } = new()
    {
        ["BorerSpawn"] = 1,
        // VentCrittersRule ignores specialEntry probability and always offers a rat king.
        ["KingRatMigration"] = 1,
    };

    // These departments are assumed to be mindshielded.
    public List<string> AntagExcludedDepartments { get; set; } = ["Security", "Command"];

    public double GlimmerStart { get; set; } = 0;
    public double GlimmerPerMinute { get; set; } = 3;

    public double SecurityWeightMultiplier { get; set; } = 1;

    // Share each round's parameter rolls across presets for comparable worlds.
    public Dictionary<string, double[]> Randomize { get; set; } = new()
    {
        ["joinWindowMinutes"] = [30, 90],
        ["joinCurve"] = [0.8, 2.5],
        ["observers"] = [0, 5],
        ["crewDeathPerMinute"] = [0.001, 0.005],
        ["antagDeathPerMinute"] = [0.0015, 0.006],
        ["revivalChance"] = [0.1, 0.6],
        ["antagOptInChance"] = [0.6, 1.0],
        ["ghostRoleTakeChance"] = [0.5, 1.0],
        ["ghostRoleTakeMedianMinutes"] = [1, 10],
        ["securityWeightMultiplier"] = [0.5, 2.0],
        ["glimmerPerMinute"] = [0, 6],
    };

    public Dictionary<string, double> ConditionPassChance { get; set; } = new()
    {
        ["MalfAiAvailableCondition"] = 0.5,
        ["BountyHunterTargetCondition"] = 1.0,
        ["SyndicateDropConsoleActiveCondition"] = 0.3,
    };

    public List<GameModeSimJob> Jobs { get; set; } =
    [
        new() { Job = "HeadOfSecurity", Weight = 1, Slots = 1 },
        new() { Job = "Warden", Weight = 1, Slots = 1 },
        new() { Job = "Detective", Weight = 1, Slots = 1 },
        new() { Job = "SecurityOfficer", Weight = 5, Slots = 5 },
        new() { Job = "SecurityCadet", Weight = 1.5, Slots = 4 },
        new() { Job = "Brigmedic", Weight = 0.5, Slots = 1 },
        new() { Job = "Captain", Weight = 2, Slots = 1 },
        new() { Job = "HeadOfPersonnel", Weight = 2, Slots = 1 },
        new() { Job = "ChiefEngineer", Weight = 1.5, Slots = 1 },
        new() { Job = "ChiefMedicalOfficer", Weight = 1.5, Slots = 1 },
        new() { Job = "ResearchDirector", Weight = 1.5, Slots = 1 },
        new() { Job = "Quartermaster", Weight = 1.5, Slots = 1 },
    ];

    public double JobWeightTotal { get; set; } = 100;

    public string FillerJob { get; set; } = "Passenger";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static GameModeSimConfig Load(string path)
    {
        if (!File.Exists(path))
            return new GameModeSimConfig();

        return JsonSerializer.Deserialize<GameModeSimConfig>(File.ReadAllText(path), JsonOptions) ?? new GameModeSimConfig();
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed class RoundParams
{
    public double JoinWindowMinutes;
    public double JoinCurve;
    public double Observers;
    public double CrewDeathPerMinute;
    public double CrewDeathPerAntagPerMinute;
    public double AntagDeathPerMinute;
    public double AntagDeathPerSecPerMinute;
    public double RevivalChance;
    public double AntagOptInChance;
    public double GhostRoleTakeChance;
    public double GhostRoleTakeMedianMinutes;
    public double SecurityWeightMultiplier;
    public double GlimmerStart;
    public double GlimmerPerMinute;

    public static readonly (string Name, Func<RoundParams, double> Get, Action<RoundParams, double> Set)[] All =
    [
        ("joinWindowMinutes", p => p.JoinWindowMinutes, (p, v) => p.JoinWindowMinutes = v),
        ("joinCurve", p => p.JoinCurve, (p, v) => p.JoinCurve = v),
        ("observers", p => p.Observers, (p, v) => p.Observers = Math.Round(v)),
        ("crewDeathPerMinute", p => p.CrewDeathPerMinute, (p, v) => p.CrewDeathPerMinute = v),
        ("crewDeathPerAntagPerMinute", p => p.CrewDeathPerAntagPerMinute, (p, v) => p.CrewDeathPerAntagPerMinute = v),
        ("antagDeathPerMinute", p => p.AntagDeathPerMinute, (p, v) => p.AntagDeathPerMinute = v),
        ("antagDeathPerSecPerMinute", p => p.AntagDeathPerSecPerMinute, (p, v) => p.AntagDeathPerSecPerMinute = v),
        ("revivalChance", p => p.RevivalChance, (p, v) => p.RevivalChance = v),
        ("antagOptInChance", p => p.AntagOptInChance, (p, v) => p.AntagOptInChance = v),
        ("ghostRoleTakeChance", p => p.GhostRoleTakeChance, (p, v) => p.GhostRoleTakeChance = v),
        ("ghostRoleTakeMedianMinutes", p => p.GhostRoleTakeMedianMinutes, (p, v) => p.GhostRoleTakeMedianMinutes = v),
        ("securityWeightMultiplier", p => p.SecurityWeightMultiplier, (p, v) => p.SecurityWeightMultiplier = v),
        ("glimmerStart", p => p.GlimmerStart, (p, v) => p.GlimmerStart = v),
        ("glimmerPerMinute", p => p.GlimmerPerMinute, (p, v) => p.GlimmerPerMinute = v),
    ];

    public static RoundParams Roll(GameModeSimConfig cfg, GameModeSimScenario scenario, Random rand)
    {
        var p = new RoundParams
        {
            JoinWindowMinutes = scenario.JoinWindowMinutes,
            JoinCurve = scenario.JoinCurve,
            Observers = cfg.Observers,
            CrewDeathPerMinute = cfg.CrewDeathPerMinute,
            CrewDeathPerAntagPerMinute = cfg.CrewDeathPerAntagPerMinute,
            AntagDeathPerMinute = cfg.AntagDeathPerMinute,
            AntagDeathPerSecPerMinute = cfg.AntagDeathPerSecPerMinute,
            RevivalChance = cfg.RevivalChance,
            AntagOptInChance = cfg.AntagOptInChance,
            GhostRoleTakeChance = cfg.GhostRoleTakeChance,
            GhostRoleTakeMedianMinutes = cfg.GhostRoleTakeMedianMinutes,
            SecurityWeightMultiplier = cfg.SecurityWeightMultiplier,
            GlimmerStart = cfg.GlimmerStart,
            GlimmerPerMinute = cfg.GlimmerPerMinute,
        };

        // Keep rolls independent of dictionary order.
        foreach (var (name, _, set) in All)
        {
            var range = cfg.Randomize.FirstOrDefault(r => string.Equals(r.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            if (range is { Length: 2 })
                set(p, range[0] + rand.NextDouble() * (range[1] - range[0]));
        }

        return p;
    }

    public static IEnumerable<string> RandomizedNames(GameModeSimConfig cfg)
    {
        return All.Select(a => a.Name)
            .Where(n => cfg.Randomize.Keys.Any(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class GameModeSimScenario
{
    public string Name { get; set; } = "40pop-15start";
    public double RoundMinutes { get; set; } = 150;

    public int Population { get; set; } = 40;

    public int RoundstartPlayers { get; set; } = 15;

    public double JoinWindowMinutes { get; set; } = 60;

    public double JoinCurve { get; set; } = 1.5;
}

public sealed class GameModeSimJob
{
    public string Job { get; set; } = "";
    public double Weight { get; set; }
    public int Slots { get; set; } = int.MaxValue;
}
