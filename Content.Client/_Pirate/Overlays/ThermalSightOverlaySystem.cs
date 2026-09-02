// SPDX-License-Identifier: MIT
using Content.Client._Pirate.Atmos.Overlays;
using Content.Client.Overlays;
using Content.Shared._Pirate.Overlays;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Overlays;

public sealed partial class ThermalSightOverlaySystem : EquipmentHudSystem<ThermalSightComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private GasTileDangerousTemperatureOverlay _temperatureOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _temperatureOverlay = new();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ThermalSightComponent> component)
    {
        base.UpdateInternal(component);

        _overlayMan.AddOverlay(_temperatureOverlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_temperatureOverlay);
    }
}
