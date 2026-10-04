using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared.VendingMachines;

public sealed partial class VendingMachineInventoryPrototype
{
    [DataField("categories", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<VendingMachineCategoryPrototype>))]
    public HashSet<string> Categories { get; private set; } = [];
}

public sealed partial class VendingMachineInventoryEntry
{
    [DataField]
    public string? Category;
}
