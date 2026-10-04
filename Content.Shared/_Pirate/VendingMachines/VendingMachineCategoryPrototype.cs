using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.VendingMachines;

[Prototype]
public sealed partial class VendingMachineCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField("startingInventory", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<uint, EntityPrototype>))]
    public Dictionary<string, uint> StartingInventory { get; private set; } = new();

    [DataField("emaggedInventory", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<uint, EntityPrototype>))]
    public Dictionary<string, uint>? EmaggedInventory { get; private set; }

    [DataField("contrabandInventory", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<uint, EntityPrototype>))]
    public Dictionary<string, uint>? ContrabandInventory { get; private set; }
}
