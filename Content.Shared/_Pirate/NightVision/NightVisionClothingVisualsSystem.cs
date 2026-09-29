// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Pirate.NightVision;

public sealed class NightVisionClothingVisualsSystem : EntitySystem
{
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionClothingVisualsComponent, MonolithNightVisionToggledEvent>(OnToggled);
        // Taking the clothing off switches the night vision off without going through SetEnabled.
        SubscribeLocalEvent<NightVisionClothingVisualsComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnToggled(Entity<NightVisionClothingVisualsComponent> ent, ref MonolithNightVisionToggledEvent args)
    {
        SetVisuals(ent, args.Enabled);
    }

    private void OnUnequipped(Entity<NightVisionClothingVisualsComponent> ent, ref GotUnequippedEvent args)
    {
        SetVisuals(ent, false);
    }

    private void SetVisuals(Entity<NightVisionClothingVisualsComponent> ent, bool enabled)
    {
        _clothing.SetEquippedPrefix(ent, enabled ? ent.Comp.EnabledPrefix : null);
        _appearance.SetData(ent, NightVisionClothingVisuals.Enabled, enabled);
    }
}
