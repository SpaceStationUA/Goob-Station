/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.Planets.Descent;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.Planets.Descent;

/// <summary>
/// Fullscreen cover for the planet descent sequence. While the local player's grid runs a
/// descent/ascent, draws a whiteout over the viewport: a gentle clouding ramp during the
/// fall, opaque white for the warp, fading back out on arrival. Reading only networked
/// stage + start time, so it always matches the server's sequence without per-tick traffic.
/// </summary>
public sealed partial class CEZDescentFadeOverlay : Overlay
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CEZDescentFadeOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player)
            return false;

        var grid = _entMan.GetComponent<TransformComponent>(player).GridUid;
        return grid != null &&
               _entMan.HasComponent<CEDescentComponent>(grid.Value);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var grid = _entMan.GetComponent<TransformComponent>(player).GridUid;
        if (grid == null ||
            !_entMan.TryGetComponent<CEDescentComponent>(grid.Value, out var descent))
        {
            return;
        }

        var progress = _timing.CurTime >= descent.StageStart
            ? (float) ((_timing.CurTime - descent.StageStart) / CESharedDescentSystem.StageDuration(descent.Stage, descent.Ascent))
            : 0f;
        progress = Math.Clamp(progress, 0f, 1f);

        // Alpha curve per stage:
        //   Descending: 0 → 0.55 (clouds closing in as the ship sinks)
        //   Vanishing:  0.55 → 1 (whiteout covers the warp)
        //   Arriving:   1 → 0 (reveal the planet)
        float alpha = descent.Stage switch
        {
            CEDescentStage.Descending => 0.55f * progress,
            CEDescentStage.Vanishing => 0.55f + 0.45f * progress,
            CEDescentStage.Arriving => 1f - progress,
            _ => 0f,
        };

        if (alpha <= 0.001f)
            return;

        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.White.WithAlpha(alpha));
    }
}
