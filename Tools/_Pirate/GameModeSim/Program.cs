// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.Pirate.GameModeSim;

internal static class Program
{
    private const string Usage = """
        Usage: dotnet run --project Tools/_Pirate/GameModeSim -- [options]

          --config PATH      config file (default Tools/_Pirate/GameModeSim/config.json)
          --presets A,B      simulate only these presets (ids or aliases), instead of the config's list
          --out DIR          output folder, relative to the repository root (default: the config's outputDir)
          --rounds N         rounds per preset and scenario (default: the config's rounds)
          -h, --help         show this help
        """;

    private static async Task<int> Main(string[] args)
    {
        var root = FindRepoRoot();
        string? configPath = null, presets = null, outputDir = null;
        int? rounds = null;
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--config": configPath = Next(); break;
                case "--presets": presets = Next(); break;
                case "--out": outputDir = Next(); break;
                case "--rounds": rounds = int.Parse(Next()); break;
                case "-h" or "--help":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown option {args[i]}");
                    Console.Error.WriteLine(Usage);
                    return 1;
            }
        }

        var config = GameModeSimConfig.Load(configPath ?? Path.Combine(root, "Tools", "_Pirate", "GameModeSim", "config.json"));
        if (presets != null)
            config.Presets = presets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (outputDir != null)
            config.OutputDir = outputDir;
        if (rounds != null)
            config.Rounds = rounds.Value;

        PoolManager.Startup();
        try
        {
            await Simulate(root, config);
            return 0;
        }
        finally
        {
            PoolManager.Shutdown();
        }
    }

    private static async Task Simulate(string root, GameModeSimConfig config)
    {
        // Extra prototypes mutate the server; do not return it to the pool.
        var extra = config.ExtraPrototypes.Count > 0;
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = extra },
            new ExternalTestContext("Game mode sim", TextWriter.Null));
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        GameModeSimData data = default!;
        await server.WaitPost(() =>
        {
            if (extra)
            {
                var changed = new Dictionary<Type, HashSet<string>>();
                foreach (var file in config.ExtraPrototypes)
                    protos.LoadString(File.ReadAllText(Path.IsPathRooted(file) ? file : Path.Combine(root, file)), true, changed);
                protos.ReloadPrototypes(changed);
            }

            data = GameModeSimData.Extract(protos, factory, config.Presets);
        });
        data.AddGhostRoleSpawns(config.GhostRoleSpawnRules);
        await pair.CleanReturnAsync();

        var outputDir = Path.IsPathRooted(config.OutputDir) ? config.OutputDir : Path.Combine(root, config.OutputDir);
        var watch = Stopwatch.StartNew();
        using var output = new GameModeSimOutput(outputDir);
        var sim = new GameModeSimulator(data, config, output);

        for (var p = 0; p < config.Presets.Count; p++)
        {
            // Seed by preset id so separate runs can be merged reproducibly.
            var presetSeed = StableHash(data.Presets[config.Presets[p]].Id);
            for (var sc = 0; sc < config.Scenarios.Count; sc++)
            {
                for (var r = 0; r < config.Rounds; r++)
                {
                    var seed = Mix(config.Seed, presetSeed, sc, r);
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

        Console.WriteLine($"Simulated {config.Presets.Count * config.Scenarios.Count * config.Rounds} rounds in {watch.Elapsed.TotalSeconds:0.0}s -> {outputDir}");
        foreach (var warning in output.Warnings)
            Console.WriteLine($"warning: {warning}");
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

    // string.GetHashCode is salted per process; zero is reserved for population seeds.
    private static int StableHash(string id)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in id)
                h = (h ^ c) * 16777619u;

            return (int) (h == 0 ? 1 : h);
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
