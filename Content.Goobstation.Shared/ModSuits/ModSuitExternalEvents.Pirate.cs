using Robust.Shared.Containers;

namespace Content.Goobstation.Shared.ModSuits;

// Pirate: module effects can use another suit controller without running a second suit system.
public readonly record struct ModSuitModuleHost(
    EntityUid Owner,
    EntityUid? Wearer,
    IReadOnlyDictionary<string, EntityUid> PartEntities,
    BaseContainer ModulesContainer);

[ByRefEvent]
public record struct QueryModSuitModuleHostEvent
{
    public ModSuitModuleHost? Host;
}

public enum ExternalModModuleCommand : byte
{
    Use,
    StartUse,
    SetActive,
}

[ByRefEvent]
public record struct ExternalModModuleCommandEvent(
    EntityUid Module,
    EntityUid? User,
    ExternalModModuleCommand Command,
    bool Active = false)
{
    public bool Succeeded;
}

[ByRefEvent]
public record struct ExternalModSuitChargeEvent(float Amount, bool Consume)
{
    public bool Succeeded;
}
