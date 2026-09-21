using Content.Pirate.Shared.Shipyard.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Shipyard;

[Serializable, NetSerializable]
public enum ShipyardConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ShipyardConsoleState(int balance) : BoundUserInterfaceState
{
    public readonly int Balance = balance;
}

[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage(string vessel) : BoundUserInterfaceMessage
{
    public readonly ProtoId<VesselPrototype> Vessel = vessel;
}
