// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Suspicion.RadarOverlay;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Suspicion.RadarOverlay;

public sealed class RadarOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    public RadarInfo[] RadarInfos = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OnRadarOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<RadarOverlayUpdatedEvent>(OnOverlayUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Clear();
    }

    public void Clear()
    {
        _overlayManager.RemoveOverlay<RadarOverlay>();
        RadarInfos = [];
    }

    private void OnOverlayToggled(OnRadarOverlayToggledEvent ev)
    {
        if (!ev.IsEnabled)
            Clear();
        else if (!_overlayManager.HasOverlay<RadarOverlay>())
            _overlayManager.AddOverlay(new RadarOverlay());
    }

    private void OnOverlayUpdate(RadarOverlayUpdatedEvent ev)
    {
        RadarInfos = ev.RadarInfos;
    }
}
