// SPDX-License-Identifier: MIT
using System.Linq;
using System.Linq;
using Content.Client._Pirate.Atmos.Overlays;
using Content.Client.Overlays;
using Content.Shared._Pirate.Overlays;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;

namespace Content.Client._Pirate.Overlays;

public sealed partial class ThermalSightOverlaySystem : EquipmentHudSystem<ThermalSightComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private GasTileDangerousTemperatureOverlay _temperatureOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _temperatureOverlay = new();

        SubscribeLocalEvent<ThermalSightComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<ThermalSightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ThermalSightComponent> component)
    {
        base.UpdateInternal(component);

        if (component.Components.Any(c => c.Enabled))
            _overlayMan.AddOverlay(_temperatureOverlay);
        else
            _overlayMan.RemoveOverlay(_temperatureOverlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_temperatureOverlay);
    }
}
