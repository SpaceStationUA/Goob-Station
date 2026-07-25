using Content.Shared.Whitelist;

namespace Content.Shared._Pirate.Clothing.Components;

/// <summary>
/// Restricts clothing to entities carrying one of the configured whitelist tags.
/// </summary>
[RegisterComponent]
public sealed partial class SpecialisedClothingComponent : Component
{
    [DataField]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public LocId? FailureReason;
}
