// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Shuttles.Warp;

/// <summary>
///     Sends an expanding ring out from every live warp drive when the warp flashes: on entry from
///     drives in transit, and on exit from drives still holding the field.
/// </summary>
/// <remarks>
///     Positions come from networked transforms and timing from the local warp state, so this needs no
///     server traffic. Reduced motion turns it off.
/// </remarks>
public sealed class WarpDriveShockSystem : EntitySystem
{
    [Dependency] private readonly IParallaxManager _parallax = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    /// <summary>
    ///     How long a pulse stays visible, in seconds. Matches the shader.
    /// </summary>
    public const float PulseDuration = 2.5f;

    private const float EntryStrength = 1f;
    private const float ExitStrength = 1.2f;

    private readonly List<ShockPulse> _pulses = new();
    private WarpDriveShockOverlay? _overlay;
    private bool _wasWarping;
    private bool _reducedMotion;

    public IReadOnlyList<ShockPulse> Pulses => _pulses;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new WarpDriveShockOverlay(this);
        _overlayManager.AddOverlay(_overlay);

        Subs.CVar(_config, CCVars.ReducedMotion, value => _reducedMotion = value, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlayManager.RemoveOverlay(_overlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime.TotalSeconds;
        _pulses.RemoveAll(p => now - p.Start > PulseDuration + 0.5);

        var warping = _parallax.IsWarping;
        if (warping && !_wasWarping)
            Emit(EntryStrength);
        else if (!warping && _wasWarping)
            Emit(ExitStrength);

        _wasWarping = warping;
    }

    private void Emit(float strength)
    {
        if (_reducedMotion)
            return;

        var query = EntityQueryEnumerator<WarpDriveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drive, out var transform))
        {
            if (drive.DriveState is not (WarpDriveState.Engaged or WarpDriveState.InTransit)
                || transform.MapID == MapId.Nullspace)
            {
                continue;
            }

            _pulses.Add(new ShockPulse(
                transform.MapID,
                _transform.GetWorldPosition(uid),
                _timing.CurTime.TotalSeconds,
                strength));
        }
    }

    public readonly record struct ShockPulse(MapId Map, Vector2 World, double Start, float Strength);
}
