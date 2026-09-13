// SPDX-License-Identifier: MIT

using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Debug;

/// <summary>
///     Dev probe: measures client round-join timeline for the wipe overlay project.
///     Logs [PROBE] lines when pirate.roundprobe=true: attach + time until frame pacing stabilizes.
///     Client-only measurements, no gameplay effect.
/// </summary>
public sealed class RoundLoadProbeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly CVarDef<bool> ProbeCvar =
        Content.Goobstation.Common.CCVar.PirateCVars.PirateRoundProbe;

    private static readonly TimeSpan StableThreshold = TimeSpan.FromMilliseconds(50);
    private const int StableFramesRequired = 10;

    private bool _enabled;
    private TimeSpan _attachTime;
    private bool _attachLogged;
    private int _stableFrames;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttach);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetach);
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        cfg.OnValueChanged(ProbeCvar, v => _enabled = v, invokeImmediately: true);
    }

    private void OnAttach(LocalPlayerAttachedEvent ev)
    {
        if (!_enabled || _attachLogged)
            return;

        _attachLogged = true;
        _attachTime = _timing.RealTime;
        Log.Info("[PROBE] attach t=0");
    }

    private void OnDetach(LocalPlayerDetachedEvent ev)
    {
        if (!_enabled)
            return;

        _attachLogged = false;
        _stableFrames = 0;
        Log.Info("[PROBE] detach");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || !_attachLogged)
            return;

        if (_timing.RealFrameTime <= StableThreshold)
            _stableFrames++;
        else
            _stableFrames = 0;

        if (_stableFrames >= StableFramesRequired)
        {
            var elapsed = _timing.RealTime - _attachTime;
            Log.Info($"[PROBE] stable after {elapsed.TotalSeconds:F2}s of attach");
            _stableFrames = int.MinValue; // one report per attach
            _enabled = false;
        }
    }
}
