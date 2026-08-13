/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.Camera;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.Camera;

/// <summary>
/// Generic client API for the "thruster blew up" style of screenshake: while a
/// <see cref="RadialShakeComponent"/> sits on the local player, throw the camera's
/// recoil offset to a fresh random point every frame, tapering off quadratically.
/// Trigger it from the server by raising a <see cref="RadialShakeEvent"/> through
/// a player filter, or from client code via <see cref="Shake"/>.
/// </summary>
/// <remarks>
/// Deliberately NOT via <see cref="SharedCameraRecoilSystem.KickCamera"/>: that is
/// a saturating accumulator (kicks are scaled by the remaining headroom under its
/// 1-tile cap), so per-frame hammering pins it at the rim where further kicks
/// multiply to zero — the camera freezes one tile off-centre instead of shaking.
/// </remarks>
public sealed partial class RadialShakeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RadialShakeEvent>(OnRadialShake);
    }

    private void OnRadialShake(RadialShakeEvent ev)
    {
        if (_player.LocalEntity is { } player)
            Shake(player, ev.Duration, ev.Amplitude);
    }

    /// <summary>
    /// Start (or reinforce) a radial shake. Overlapping shakes merge: the envelope
    /// keeps whichever end time is later and whichever amplitude is bigger.
    /// </summary>
    public void Shake(EntityUid uid, float duration, float amplitude)
    {
        if (duration <= 0f || amplitude <= 0f)
            return;

        var shake = EnsureComp<RadialShakeComponent>(uid);
        var now = _timing.CurTime;

        if (now + TimeSpan.FromSeconds(duration) >= shake.Start + TimeSpan.FromSeconds(shake.Duration))
        {
            shake.Start = now;
            shake.Duration = duration;
        }

        shake.Amplitude = MathF.Max(shake.Amplitude, amplitude);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player
            || !TryComp<RadialShakeComponent>(player, out var shake))
        {
            return;
        }

        var elapsed = (float) (_timing.CurTime - shake.Start).TotalSeconds;
        if (elapsed < 0f || elapsed >= shake.Duration)
        {
            RemCompDeferred<RadialShakeComponent>(player);
            return;
        }

        if (!TryComp<CameraRecoilComponent>(player, out var recoil))
            return;

        // Quadratic taper: full fury up front, dying twitches by the end.
        var taper = 1f - elapsed / shake.Duration;
        taper *= taper;

        // Shake is *motion*, so place the offset absolutely each frame; the recoil
        // system's restore loop cleans up whatever's left once the taper hits zero.
        var amplitude = Math.Min(shake.Amplitude * taper * _cfg.GetCVar(CCVars.ScreenShakeIntensity), 1f);
        recoil.CurrentKick = _random.NextAngle().ToVec() * amplitude;
        recoil.LastKickTime = 0f;
    }
}
