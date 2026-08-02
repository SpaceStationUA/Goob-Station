// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Robust.Client.Graphics;

namespace Content.Pirate.Client.Skia;

public sealed class SkiaScreechShockWaveSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private SkiaScreechShockWaveOverlay _overlay = default!;
    private int _activeEffects;
    private bool _overlayAdded;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SkiaScreechShockWaveOverlay();
        SubscribeLocalEvent<SkiaScreechShockWaveComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SkiaScreechShockWaveComponent, ComponentShutdown>(OnComponentShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnComponentInit(Entity<SkiaScreechShockWaveComponent> entity, ref ComponentInit args)
    {
        _activeEffects++;
        AddOverlay();
    }

    private void OnComponentShutdown(Entity<SkiaScreechShockWaveComponent> entity, ref ComponentShutdown args)
    {
        _activeEffects = Math.Max(0, _activeEffects - 1);
        if (_activeEffects == 0)
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
