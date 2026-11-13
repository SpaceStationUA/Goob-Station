// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.Light;

public sealed class SpriteAmbientOcclusionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private OverlayResourceCache<SpriteAmbientOcclusionResources>? _resources;
    private readonly List<Overlay> _overlays = new();

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.PirateSpriteAmbientOcclusion, OnEnabledChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlays();
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (enabled)
            AddOverlays();
        else
            RemoveOverlays();
    }

    private void AddOverlays()
    {
        if (_overlays.Count > 0)
            return;

        _resources = new OverlayResourceCache<SpriteAmbientOcclusionResources>();
        // One class per overlay: the overlay manager keys overlays by type and refuses duplicates silently.
        _overlays.Add(new SpriteAmbientOcclusionFloorCaptureOverlay(_resources));
        _overlays.Add(new SpriteAmbientOcclusionBeforeMobsCaptureOverlay(_resources));
        _overlays.Add(new SpriteAmbientOcclusionAfterMobsCaptureOverlay(_resources));
        _overlays.Add(new SpriteAmbientOcclusionOverlay(_resources));

        foreach (var overlay in _overlays)
        {
            if (!_overlayMan.AddOverlay(overlay))
                Log.Error($"Could not add {overlay.GetType().Name}: an overlay of that type already exists.");
        }
    }

    private void RemoveOverlays()
    {
        foreach (var overlay in _overlays)
        {
            _overlayMan.RemoveOverlay(overlay);
        }

        _overlays.Clear();
        _resources?.Dispose();
        _resources = null;
    }
}
