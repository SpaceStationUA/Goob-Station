// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Overlays;
using Content.Goobstation.Client.Overlays;
using Content.Goobstation.Shared.Overlays;
using Content.Pirate.Shared.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Pirate.Client.Overlays;

public sealed class SharkVisionSystem : EquipmentHudSystem<SharkVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private SharkVisionOverlay _sharkOverlay = default!;
    private BaseSwitchableOverlay<SharkVisionComponent> _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharkVisionComponent, SwitchableOverlayToggledEvent>(OnToggle);

        _sharkOverlay = new SharkVisionOverlay();
        _overlay = new BaseSwitchableOverlay<SharkVisionComponent>
        {
            RestrictToPlayerViewport = true
        };
    }

    protected override void OnRefreshComponentHud(Entity<SharkVisionComponent> ent,
        ref RefreshEquipmentHudEvent<SharkVisionComponent> args)
    {
        if (!ent.Comp.IsEquipment)
            base.OnRefreshComponentHud(ent, ref args);
    }

    protected override void OnRefreshEquipmentHud(Entity<SharkVisionComponent> ent,
        ref InventoryRelayedEvent<RefreshEquipmentHudEvent<SharkVisionComponent>> args)
    {
        if (ent.Comp.IsEquipment)
            base.OnRefreshEquipmentHud(ent, ref args);
    }

    private void OnToggle(Entity<SharkVisionComponent> ent, ref SwitchableOverlayToggledEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<SharkVisionComponent> args)
    {
        base.UpdateInternal(args);
        SharkVisionComponent? svComp = null;
        foreach (var comp in args.Components)
        {
            if (!comp.IsActive && (comp.PulseTime <= 0f || comp.PulseAccumulator >= comp.PulseTime))
                continue;

            if (svComp == null)
                svComp = comp;
            else if (!svComp.DrawOverlay && comp.DrawOverlay)
                svComp = comp;
            else if (svComp.DrawOverlay == comp.DrawOverlay && svComp.PulseTime > 0f && comp.PulseTime <= 0f)
                svComp = comp;
        }

        UpdateSharkOverlay(svComp);
        UpdateOverlay(svComp);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        UpdateOverlay(null);
        UpdateSharkOverlay(null);
    }

    private void UpdateSharkOverlay(SharkVisionComponent? comp)
    {
        _sharkOverlay.Comp = comp;

        switch (comp)
        {
            case not null when !_overlayMan.HasOverlay<SharkVisionOverlay>():
                _overlayMan.AddOverlay(_sharkOverlay);
                break;
            case null:
                _overlayMan.RemoveOverlay(_sharkOverlay);
                break;
        }
    }

    private void UpdateOverlay(SharkVisionComponent? svComp)
    {
        _overlay.Comp = svComp;

        switch (svComp)
        {
            case { DrawOverlay: true } when !_overlayMan.HasOverlay<BaseSwitchableOverlay<SharkVisionComponent>>():
                _overlayMan.AddOverlay(_overlay);
                break;
            case null or { DrawOverlay: false }:
                _overlayMan.RemoveOverlay(_overlay);
                break;
        }

        // Prefer night vision.
        _overlay.IsActive = !_overlayMan.HasOverlay<BaseSwitchableOverlay<NightVisionComponent>>();
    }
}
