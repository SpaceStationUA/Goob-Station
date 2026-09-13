// SPDX-License-Identifier: MIT

using Robust.Client.ResourceManagement;
using SixLabors.ImageSharp;
using System.IO;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Debug;

/// <summary>
///     Dev probe: measures client round-join timeline for the wipe overlay project.
///     Logs [PROBE] lines when pirate.roundprobe=true: attach + time until frame pacing stabilizes.
///     Also dumps a PNG to /tmp/wipe-*.png at attach+delay to verify overlay rendering.
///     Client-only measurements, no gameplay effect.
/// </summary>
public sealed class RoundLoadProbeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly CVarDef<bool> ProbeCvar =
        Content.Goobstation.Common.CCVar.PirateCVars.PirateRoundProbe;

    private static readonly TimeSpan StableThreshold = TimeSpan.FromMilliseconds(50);
    private const int StableFramesRequired = 10;

    private static readonly TimeSpan[] ShotOffsets =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1.25),
        TimeSpan.FromSeconds(1.5),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2.5),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(3.5),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(4.5)
    ];

    private bool _enabled;
    private TimeSpan _attachTime;
    private bool _attachLogged;
    private int _stableFrames;
    private int _nextShot;

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
        _nextShot = 0;
        Log.Info("[PROBE] attach t=0");
    }

    private void OnDetach(LocalPlayerDetachedEvent ev)
    {
        if (!_enabled)
            return;

        _attachLogged = false;
        _stableFrames = 0;
        _nextShot = 0;
        Log.Info("[PROBE] detach");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || !_attachLogged)
            return;

        var since = _timing.RealTime - _attachTime;

        if (_nextShot < ShotOffsets.Length && since >= ShotOffsets[_nextShot])
        {
            Capture(_nextShot, since);
            _nextShot++;
        }

        if (_timing.RealFrameTime <= StableThreshold)
            _stableFrames++;
        else
            _stableFrames = 0;

        if (_stableFrames >= StableFramesRequired)
        {
            Log.Info($"[PROBE] stable after {since.TotalSeconds:F2}s of attach");
            _stableFrames = int.MinValue;
        }
    }

    private void Capture(int idx, TimeSpan since)
    {
        try
        {
            IoCManager.Resolve<IClyde>().Screenshot(ScreenshotType.Final, img =>
            {
                try
                {
                    var res = IoCManager.Resolve<IResourceManager>();
                    var path = new Robust.Shared.Utility.ResPath($"/Screenshots/wipe-shot{idx}.png");
                    using var fs = res.UserData.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    img.SaveAsPng(fs);
                    Log.Info($"[PROBE] screenshot {idx} at {since.TotalSeconds:F2}s -> {path}");
                }
                catch (Exception e)
                {
                    Log.Warning($"[PROBE] screenshot fork failed: {e.Message}");
                }
            });
        }
        catch (Exception e)
        {
            Log.Warning($"[PROBE] screenshot failed: {e.Message}");
        }
    }
}
