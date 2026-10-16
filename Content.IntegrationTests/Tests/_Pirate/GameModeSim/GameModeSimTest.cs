#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.GameModeSim;

[TestFixture]
[Explicit]
public sealed class GameModeSimTest
{
    [Test]
    public async Task SimulateGamePresets()
    {
        var root = FindRepoRoot();
        var configPath = Environment.GetEnvironmentVariable("GAMEMODE_SIM_CONFIG")
                         ?? Path.Combine(root, "Tools", "_Pirate", "GameModeSim", "config.json");
        var config = GameModeSimConfig.Load(configPath);

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        GameModeSimData data = default!;
        await server.WaitPost(() => data = GameModeSimData.Extract(protos, factory, config.Presets));
        data.AddGhostRoleSpawns(config.GhostRoleSpawnRules);
        await pair.CleanReturnAsync();

        var outputDir = Path.IsPathRooted(config.OutputDir) ? config.OutputDir : Path.Combine(root, config.OutputDir);
        var watch = Stopwatch.StartNew();
        using var output = new GameModeSimOutput(outputDir);
        var sim = new GameModeSimulator(data, config, output);

        for (var p = 0; p < config.Presets.Count; p++)
        {
            for (var sc = 0; sc < config.Scenarios.Count; sc++)
            {
                for (var r = 0; r < config.Rounds; r++)
                {
                    var seed = Mix(config.Seed, 1 + p, sc, r);
                    // All presets must see the same population rolls.
                    var paramSeed = Mix(config.Seed, 0, sc, r);
                    sim.RunRound(config.Presets[p], config.Scenarios[sc], r, seed, paramSeed);
                }
            }
        }

        var meta = new
        {
            generated = DateTime.UtcNow,
            seconds = watch.Elapsed.TotalSeconds,
            config,
            randomized = RoundParams.RandomizedNames(config),
            presets = data.Presets.Values.Select(p => new
            {
                id = p.Id,
                chaosSchedulers = p.Chaos.Select(c => c.Id),
                basicSchedulers = p.Basic.Select(b => b.Id),
                unsimulated = p.Unsimulated,
            }),
            warnings = output.Warnings,
        };
        File.WriteAllText(Path.Combine(outputDir, "meta.json"),
            JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));

        TestContext.Out.WriteLine($"Simulated {config.Presets.Count * config.Scenarios.Count * config.Rounds} rounds in {watch.Elapsed.TotalSeconds:0.0}s -> {outputDir}");
        foreach (var warning in output.Warnings)
            TestContext.Out.WriteLine($"warning: {warning}");
    }

    // HashCode.Combine is salted per process.
    private static int Mix(params int[] parts)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var part in parts)
            {
                h = (h ^ (uint) part) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
            }

            return (int) h;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Resources", "Prototypes")))
            dir = dir.Parent;

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
