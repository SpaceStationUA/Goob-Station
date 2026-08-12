/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._FarHorizons.Planets.Descent;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Client._FarHorizons.Planets.Descent;

/// <summary>
/// Client half of the descent sequence: the fade overlay registration plus the per-frame
/// feedback — zooming the rider's eye out during the fall, and the radial screenshake
/// when the drive discharges.
/// </summary>
public sealed partial class CEDescentSystem : CESharedDescentSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    /// <summary>Peak shake amplitude in tiles. Perceived shake scales linearly with this,
    /// hard-limited to 1 (the eye-offset range the recoil system was tuned for).</summary>
    private const float ShakeAmplitude = 1.2f;

    /// <summary>How far the rider's eye zooms out during the fall, and back in on arrival.</summary>
    private const float DescentZoom = 2.25f;

    private CEZDescentFadeOverlay _fadeOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _fadeOverlay = new CEZDescentFadeOverlay();
        _overlay.AddOverlay(_fadeOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZDescentFadeOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player)
            return;

        var grid = Transform(player).GridUid;

        // Descent zoom: zoom the eye out while the ship falls, ease back in on arrival.
        if (grid != null && TryComp<CEDescentComponent>(grid.Value, out var descent))
        {
            if (TryComp<EyeComponent>(player, out var eye))
            {
                var progress = GetStageProgress(descent.Stage, descent.StageStart, descent.Ascent);
                var target = descent.Stage switch
                {
                    CEDescentStage.Arriving => 1f + (DescentZoom - 1f) * (1f - progress),
                    _ => DescentZoom,
                };
                var zoom = Vector2.Lerp(eye.Zoom, new Vector2(target, target), Math.Clamp(frameTime * 4f, 0f, 1f));
                _eye.SetZoom(player, zoom);
            }
        }

        // Discharge shake: while the local player's grid carries a fresh stun, throw the
        // camera's recoil offset to a fresh random point every frame, tapering off
        // quadratically over DischargeStunTime.
        if (grid is not { } stunGrid ||
            !TryComp<CEDescentStunnedComponent>(stunGrid, out var stunned))
        {
            return;
        }

        var elapsed = (float) (Timing.CurTime - stunned.Start).TotalSeconds;
        var duration = (float) DischargeStunTime.TotalSeconds;
        if (elapsed < 0f || elapsed >= duration)
            return;

        var taper = 1f - elapsed / duration;
        taper *= taper;

        if (!TryComp<CameraRecoilComponent>(player, out var recoil))
            return;

        var amplitude = MathF.Min(ShakeAmplitude * taper * _cfg.GetCVar(CCVars.ScreenShakeIntensity), 1f);
        recoil.CurrentKick = _random.NextAngle().ToVec() * amplitude;
        recoil.LastKickTime = 0f;
    }
}
