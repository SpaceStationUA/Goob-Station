// SPDX-License-Identifier: MIT

using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Pirate.Experiments.Light;

public sealed class SharedFlashImmunityTogglePointLightSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    // Pirate: Starlight's FlashImmunityCheckEvent is unavailable here. Polling also handles visor toggles.
    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlashImmunityTogglePointLightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FlashImmunityTogglePointLightComponent, ClothingDidEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FlashImmunityTogglePointLightComponent, ClothingDidUnequippedEvent>(OnUnequipped);
    }

    private void OnStartup(Entity<FlashImmunityTogglePointLightComponent> ent, ref ComponentStartup args) => UpdateLight(ent);
    private void OnEquipped(Entity<FlashImmunityTogglePointLightComponent> ent, ref ClothingDidEquippedEvent args) => UpdateLight(ent);
    private void OnUnequipped(Entity<FlashImmunityTogglePointLightComponent> ent, ref ClothingDidUnequippedEvent args) => UpdateLight(ent);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _updateTimer += frameTime;
        if (_updateTimer < 0.25f)
            return;
        _updateTimer = 0;
        var query = EntityQueryEnumerator<FlashImmunityTogglePointLightComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateLight((uid, comp));
    }

    private void UpdateLight(Entity<FlashImmunityTogglePointLightComponent> ent)
    {
        if (!_light.TryGetLight(ent, out var light))
            return;

        var immune = HasProtection(ent);
        if (TryComp<InventoryComponent>(ent, out var inventory))
        {
            var slots = _inventory.GetSlotEnumerator((ent.Owner, inventory), SlotFlags.WITHOUT_POCKET);
            while (!immune && slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item)
                    immune = HasProtection(item);
            }
        }
        _light.SetEnabled(ent, ent.Comp.Invert ? !immune : immune, light);
    }

    private bool HasProtection(EntityUid uid)
    {
        if (!TryComp<FlashImmunityComponent>(uid, out var immunity) || !immunity.Enabled)
            return false;
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return false;
        return true; // Pirate: upstream clothing toggles remove FlashImmunity while raised.
    }
}
