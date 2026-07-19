using Content.Pirate.Shared.Overlays.Shockwave;
using Robust.Client.Graphics;

namespace Content.Pirate.Client.Overlays.Shockwave;

/// <summary>
/// Manages the shockwave fullscreen overlay while shockwave entities exist.
/// </summary>
public sealed partial class ClientShockwaveSystem : SharedShockwaveSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private ShockwaveOverlay _overlay = default!;
    private int _activeShockwaves;
    private bool _overlayAdded;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ShockwaveOverlay();

        SubscribeLocalEvent<ShockwaveComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<ShockwaveComponent, ComponentShutdown>(OnCompShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnCompInit(Entity<ShockwaveComponent> entity, ref ComponentInit args)
    {
        _activeShockwaves++;
        AddOverlay();
    }

    private void OnCompShutdown(Entity<ShockwaveComponent> entity, ref ComponentShutdown args)
    {
        _activeShockwaves = Math.Max(0, _activeShockwaves - 1);

        if (_activeShockwaves == 0)
            RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlayAdded)
            return;

        _overlayManager.AddOverlay(_overlay);
        _overlayAdded = true;
    }

    private void RemoveOverlay()
    {
        if (!_overlayAdded)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlayAdded = false;
    }
}
