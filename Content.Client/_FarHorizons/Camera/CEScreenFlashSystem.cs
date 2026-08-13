/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.Camera;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.Camera;

/// <summary>
/// Client end of <see cref="CEScreenFlashEvent"/>: keeps a single fullscreen
/// <see cref="CEScreenFlashOverlay"/> alive while any flash envelope is running.
/// Overlapping flashes merge; the overlay is dropped once the last one fades out.
/// </summary>
public sealed class CEScreenFlashSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;

    private CEScreenFlashOverlay? _instance;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CEScreenFlashEvent>(OnScreenFlash);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_instance is not null)
            _overlay.RemoveOverlay(_instance);
        _instance = null;
    }

    private void OnScreenFlash(CEScreenFlashEvent ev)
    {
        Flash(ev.Duration, ev.Color);
    }

    /// <summary>Client-side API: flood the screen for <paramref name="duration"/> seconds.</summary>
    public void Flash(float duration, Color color)
    {
        if (duration <= 0f || color.A <= 0f)
            return;

        if (_instance is null)
        {
            _instance = new CEScreenFlashOverlay(_timing);
            _overlay.AddOverlay(_instance);
        }

        _instance.Merge(_timing.CurTime, duration, color);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_instance is null || !_instance.Finished(_timing.CurTime))
            return;

        _overlay.RemoveOverlay(_instance);
        _instance = null;
    }
}
