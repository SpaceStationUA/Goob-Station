// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Client._Pirate.ZLevels.Lighting;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;

namespace Content.Client._Pirate.ZLevels.Profiling;

/// <summary>
/// Profiling helper: records real frame times once the local player is in game and logs
/// avg/p50/p95/p99 per phase with a ZPROF_FPS prefix, so a script can grep the client log.
/// Each extra phase applies its cvars (comma-separated name=value) and is measured again in the same spot.
/// Results are also written to user data /zprof_fps.log. Phase actions besides cvars:
/// goto=N (zprof_goto), walk=dx:dy:seconds (zprof_walk), cmd=server+command (spaces as '+'), wait=seconds,
/// shot=name (viewport-only PNG in /zprof_shots once the phase settles, plus the z-region decisions).
/// </summary>
public sealed class ZProfFrameStatsUIController : UIController
{
    private const double PhaseSettleSeconds = 3;
    private const double GotoSettleSeconds = 8;
    private static readonly ResPath OutputPath = new("/zprof_fps.log");
    private static readonly ResPath ShotDir = new("/zprof_shots");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly ILogManager _logs = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private readonly Queue<string> _phases = new();
    private readonly List<double> _samples = new();
    private ISawmill _sawmill = default!;
    private string? _current;
    private string? _pendingShot;
    private double _wait;
    private double _sampleSeconds;
    private double _elapsed;
    private bool _sampling;
    private int _phaseIndex;
    private int _regionSamples;
    private double _cropPasses;
    private double _fullLowerPasses;
    private double _lowerTargetArea;
    private double _cropAllocations;
    private double _wholeStackSkips;
    private double _fusedBlurPasses;
    private double _blurFusionFallbacks;
    private double _cropSizeTransitions;
    private double _lightConsidered;
    private double _lightPositions;
    private double _lightIndexRebuilds;
    private int _lightComparisons;
    private int _lightMismatches;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logs.GetSawmill("zprof");
    }

    public void Start(double warmupSeconds, double sampleSeconds, IEnumerable<string> phases)
    {
        _phases.Clear();
        _phases.Enqueue("-");
        foreach (var phase in phases)
            _phases.Enqueue(phase);

        _sampleSeconds = sampleSeconds;
        _phaseIndex = -1;
        _res.UserData.WriteAllText(OutputPath, string.Empty);
        NextPhase(warmupSeconds);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (_current == null || _player.LocalEntity == null)
            return;

        var dt = _timing.RealFrameTime.TotalSeconds;
        _elapsed += dt;

        if (!_sampling)
        {
            if (_elapsed < _wait)
                return;

            if (_pendingShot != null)
            {
                TakeShot(_pendingShot);
                _pendingShot = null;
            }

            _sampling = true;
            _elapsed = 0;
            _samples.Clear();
            _regionSamples = 0;
            _cropPasses = _fullLowerPasses = _lowerTargetArea = _cropAllocations = _wholeStackSkips = 0;
            _fusedBlurPasses = _blurFusionFallbacks = _cropSizeTransitions = 0;
            _lightConsidered = _lightPositions = _lightIndexRebuilds = 0;
            _lightComparisons = _lightMismatches = 0;
            return;
        }

        _samples.Add(dt * 1000);
        // Rendering follows UI FrameUpdate, so these describe the previous rendered frame.
        // Settled phase averages remain useful without synchronizing the renderer or GPU.
        if (UIManager.ActiveScreen?.GetWidget<MainViewport>()?.Viewport is { } viewport)
        {
            var stats = viewport.ZRegionStats;
            _regionSamples++;
            _cropPasses += stats.CropPasses;
            _fullLowerPasses += stats.FullLayers;
            _lowerTargetArea += stats.LowerTargetArea;
            _cropAllocations += stats.Allocations;
            _wholeStackSkips += stats.WholeStackSkipped ? 1 : 0;
            _fusedBlurPasses += stats.FusedBlurPasses;
            _blurFusionFallbacks += stats.BlurFusionFallbacks;
            _cropSizeTransitions += stats.CropSizeTransitions;
            var lights = _entities.System<CMUZLevelProjectedLightingSystem>().SourceStats;
            _lightConsidered += lights.Considered;
            _lightPositions += lights.Positions;
            _lightIndexRebuilds += lights.IndexRebuilds;
            _lightComparisons += lights.Compared ? 1 : 0;
            _lightMismatches += lights.Mismatch ? 1 : 0;
        }
        if (_elapsed < _sampleSeconds)
            return;

        Report();
        NextPhase(PhaseSettleSeconds);
    }

    private void NextPhase(double wait)
    {
        _sampling = false;
        _elapsed = 0;
        _wait = wait;

        if (!_phases.TryDequeue(out _current))
        {
            _current = null;
            Emit("ZPROF_FPS done");
            return;
        }

        _phaseIndex++;
        if (_current == "-")
            return;

        foreach (var pair in _current.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0)
                continue;

            var (name, value) = (pair[..eq], pair[(eq + 1)..]);
            if (name == "goto")
            {
                // Server-side zprof_goto; give PVS time to stream the new area in.
                _console.RemoteExecuteCommand(null, $"zprof_goto {value}");
                _wait = Math.Max(_wait, GotoSettleSeconds);
                continue;
            }

            if (name == "walk")
            {
                var parts = value.Split(':');
                if (parts.Length == 3 &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    _console.RemoteExecuteCommand(null, $"zprof_walk {parts[0]} {parts[1]} {parts[2]}");
                    _wait = Math.Max(_wait, seconds + 1);
                }
                continue;
            }

            if (name == "cmd")
            {
                _console.RemoteExecuteCommand(null, value.Replace('+', ' '));
                continue;
            }

            if (name == "wait")
            {
                // Exact settle override, e.g. to capture mid-walk.
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var exactWait))
                    _wait = exactWait;
                continue;
            }

            if (name == "shot")
            {
                _pendingShot = value;
                continue;
            }

            _console.ExecuteCommand($"cvar {name} {value}");
        }
    }

    private void Report()
    {
        var sorted = _samples.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
            return;

        double Pct(double p) => sorted[Math.Min(sorted.Length - 1, (int) (p * sorted.Length))];

        var avg = sorted.Average();
        var depth = "none";
        var mapName = "?";
        var pos = "?";
        if (_player.LocalEntity is { } uid &&
            _entities.TryGetComponent(uid, out TransformComponent? xform) &&
            xform.MapUid is { } mapUid)
        {
            mapName = _entities.GetComponent<MetaDataComponent>(mapUid).EntityName;
            pos = $"{xform.LocalPosition.X:0},{xform.LocalPosition.Y:0}";
            if (_entities.TryGetComponent(mapUid, out CEZLevelMapComponent? zMap))
                depth = zMap.Depth.ToString(CultureInfo.InvariantCulture);
        }

        string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var regional = _regionSamples == 0 ? "" :
            $" crop_passes={F(_cropPasses / _regionSamples)} full_lower_passes={F(_fullLowerPasses / _regionSamples)}" +
            $" lower_target_area={F(_lowerTargetArea / _regionSamples)} crop_allocations={(long) _cropAllocations}" +
            $" stack_skip_fraction={F(_wholeStackSkips / _regionSamples)}" +
            $" fused_blur_passes={F(_fusedBlurPasses / _regionSamples)}" +
            $" blur_fusion_fallbacks={F(_blurFusionFallbacks / _regionSamples)}" +
            $" crop_size_transitions={F(_cropSizeTransitions / _regionSamples)}" +
            $" light_considered={F(_lightConsidered / _regionSamples)} light_positions={F(_lightPositions / _regionSamples)}" +
            $" light_index_rebuilds={(long) _lightIndexRebuilds} light_comparisons={_lightComparisons} light_mismatches={_lightMismatches}";
        Emit($"ZPROF_FPS phase={_phaseIndex} cvars={_current} frames={sorted.Length} " +
             $"avg_ms={F(avg)} p50_ms={F(Pct(0.5))} p95_ms={F(Pct(0.95))} p99_ms={F(Pct(0.99))} " +
             $"max_ms={F(sorted[^1])} fps={F(1000 / avg)} map=\"{mapName}\" depth={depth} pos={pos} " +
             $"entities={_entities.EntityCount}{regional}");
    }

    private void TakeShot(string name)
    {
        if (UIManager.ActiveScreen?.GetWidget<MainViewport>()?.Viewport is not { } viewport)
            return;

        var file = ShotDir / $"{_phaseIndex:00}_{name}.png";
        // Region decisions of the frame just rendered; the capture is the next one, which matches while standing still.
        Emit($"ZPROF_SHOT phase={_phaseIndex} name={name} cvars={_current} {viewport.DescribeZRegions()}");
        viewport.Screenshot(image =>
        {
            _res.UserData.CreateDir(ShotDir);
            using var stream = _res.UserData.OpenWrite(file);
            image.SaveAsPng(stream);
        });
    }

    private void Emit(string line)
    {
        _sawmill.Info(line);
        _res.UserData.AppendAllText(OutputPath, line + "\n");
    }
}

public sealed class ZProfFpsCommand : LocalizedCommands
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override string Command => "zprof_fps";
    public override string Description => "Logs frame-time stats (ZPROF_FPS) after a warmup, optionally re-measuring with cvar changes.";
    public override string Help => "zprof_fps <warmupSeconds> <sampleSeconds> [name=value|goto=N[,...] ...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 ||
            !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var warmup) ||
            !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sample))
        {
            shell.WriteError(Help);
            return;
        }

        _ui.GetUIController<ZProfFrameStatsUIController>().Start(warmup, sample, args.Skip(2));
        shell.WriteLine($"zprof_fps: measuring {args.Length - 1} phase(s) of {sample}s after {warmup}s in game.");
    }
}
