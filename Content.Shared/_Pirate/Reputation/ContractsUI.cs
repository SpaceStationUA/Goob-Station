using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Reputation;

[Serializable, NetSerializable]
public enum ContractsUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ContractsState(
    int reputation,
    string? level,
    List<ContractSlot> contractSlots,
    List<OfferingSlot> offeringSlots) : BoundUserInterfaceState
{
    public readonly int Reputation = reputation;
    public readonly string? Level = level;
    public readonly List<ContractSlot> ContractSlots = contractSlots;
    public readonly List<OfferingSlot> OfferingSlots = offeringSlots;
}

/// <summary>
/// Accept a contract with offerings index.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContractsAcceptMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

/// <summary>
/// Complete a contract whose objective has been completed, with slot index.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContractsCompleteMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

/// <summary>
/// Rejects a contract offering with offerings index.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContractsRejectMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class PdaShowContractsMessage : BoundUserInterfaceMessage;

/// <summary>
/// Opens the contract hub from a non-PDA uplink, such as a pen.
/// </summary>
[Serializable, NetSerializable]
public sealed class StoreShowContractsMessage : BoundUserInterfaceMessage;
