// Pirate: Starlight alternative suit-storage dependencies (suit or integral frame).
using Content.Shared.Armor;

namespace Content.Shared.Inventory;

public sealed partial class SlotDefinition
{
    [DataField] public List<string> DependsOnAny = new();
}

public partial class InventorySystem
{
    private bool AreSlotDependenciesMet(EntityUid target, EntityUid item, SlotDefinition slot, InventoryComponent inventory)
    {
        if (slot.DependsOn != null && !IsDependencySlotValid(target, item, slot, slot.DependsOn, inventory))
            return false;
        if (slot.DependsOnAny.Count == 0)
            return true;
        foreach (var dependency in slot.DependsOnAny)
            if (IsDependencySlotValid(target, item, slot, dependency, inventory))
                return true;
        return false;
    }

    private bool IsDependencySlotValid(EntityUid target, EntityUid item, SlotDefinition slot, string dependency, InventoryComponent inventory)
    {
        if (!TryGetSlotEntity(target, dependency, out var equipment, inventory))
            return false;
        if (slot.DependsOnComponents == null)
            return true;
        foreach (var (_, entry) in slot.DependsOnComponents)
        {
            if (!HasComp(equipment, entry.Component.GetType()))
                return false;
            if (TryComp<AllowSuitStorageComponent>(equipment, out var storage) &&
                _whitelistSystem.IsWhitelistFailOrNull(storage.Whitelist, item))
                return false;
        }
        return true;
    }
}
