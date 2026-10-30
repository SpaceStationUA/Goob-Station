# Game-mode simulator

This tool simulates many rounds of a game preset (Entropy, Redshift, Secret TP, Blueshift and others).
Then it builds a dashboard that shows what spawns and when: antags, events, chaos budget and the gaps between antag arrivals.

The tool starts a server only to read the live prototypes. Then it plays the rounds without a game world.

The tool ports the scheduler logic line by line from the game: Secret+, Secret Salt+, Secret TP,
the basic event scheduler, `CanRun` and the antag counts. The tool does not simulate real people.
Joins, deaths, revivals, jobs and ghost-role takeovers are assumptions that you set in `config.json`.
Thus, compare the modes with each other. Do not compare the numbers with real rounds as absolute values.

## How to run the simulator

Do these steps from the repository root:

1. Run the simulator:

   ```bash
   dotnet run --project Tools/_Pirate/GameModeSim
   ```

   The simulator writes `rounds.csv`, `events.csv`, `timeline.csv` and `meta.json` to `output/`.

2. Build the dashboard:

   ```bash
   python Tools/_Pirate/GameModeSim/build_dashboard.py
   ```

   The script writes `output/dashboard.html`. This is one file. You can open it from the disk.

A full run (1,000 rounds × 3 scenarios × 5 presets) takes approximately 90 seconds. The server start adds more time.

## Options

| Option | Effect |
| --- | --- |
| `--config PATH` | Uses a different config file. |
| `--presets A,B` | Simulates only these presets (ids or aliases). |
| `--out DIR` | Writes the results to this folder. The path is relative to the repository root. |
| `--rounds N` | Sets the number of rounds for each preset and scenario. |
| `-h`, `--help` | Shows the options. |

Put the options after `--`:

```bash
dotnet run --project Tools/_Pirate/GameModeSim -- --presets Blueshift
```

## How to run one mode again

The seed of each round comes from the preset id. Thus a mode that runs alone plays the same rounds as in a full run.
When you change the prototypes of one mode, you can run only that mode again.
Then you merge its results with the earlier results.

1. Run only the changed mode, into a separate folder:

   ```bash
   dotnet run --project Tools/_Pirate/GameModeSim -- --presets Blueshift --out Tools/_Pirate/GameModeSim/output-bs
   ```

2. Build the dashboard from the two folders:

   ```bash
   python Tools/_Pirate/GameModeSim/build_dashboard.py Tools/_Pirate/GameModeSim/output Tools/_Pirate/GameModeSim/output-bs --out Tools/_Pirate/GameModeSim/output-merged
   ```

For each mode, the last folder that contains the mode supplies its results.
The script stops if the folders have a different seed, number of rounds or scenarios.
The script shows a warning if the population model is different.

## Config

`config.json` has comments on its fields. If you do not set a field, the simulator uses the default value from `GameModeSimConfig.cs`.

- `presets`, `rounds`, `seed` and `scenarios`. A scenario sets the population, the number of players at roundstart and the round length.
- `randomize`: ranges that the simulator rolls again for each round. All presets get the same roll, so all modes see the same worlds.
  A key in this list overrides the fixed value. This also applies to the join window and the join curve of a scenario.
- `extraPrototypes`: YAML files that the simulator loads on top of the game prototypes. Use them to simulate a mode from an unmerged PR.
  `extra/redshift-1.4.yml` is PR #1170 (Secret Salt 1.4) as the preset `SecretSalt14`. The game does not load this file.
  When PR #1170 is merged or closed, remove the file from `presets` and from `extraPrototypes`.
- `ghostRoleSpawnRules`: rules that spawn ghost-role antags without antag selection. The simulator counts these mobs as antags.
- `conditionPassChance`: the pass chance for table conditions that need the world state. The simulator cannot evaluate these conditions.

## Files

| File | Contents |
| --- | --- |
| `Program.cs` | The entry point: options, server start, the round loop and the seeds. |
| `GameModeSimData.cs` | Reads the presets, rules and tables from the prototypes. |
| `GameModeSimulator.cs` | The round model and the scheduler logic from the game. |
| `GameModeSimConfig.cs` | The config fields and their default values. |
| `GameModeSimOutput.cs` | Writes the CSV files. |
| `build_dashboard.py`, `dashboard.template.html` | The dashboard. The script uses only the Python standard library. |

When the scheduler code in the game changes, update `GameModeSimulator.cs` manually.
