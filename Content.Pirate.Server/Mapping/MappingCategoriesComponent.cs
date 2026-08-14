namespace Content.Pirate.Server.Mapping;

/// <summary>
/// DeltaV mapping category metadata used by ported prototypes.
/// </summary>
[RegisterComponent]
public sealed partial class MappingCategoriesComponent : Component
{
    // Pirate: data-only compatibility; local mapping validation does not gate maps by these categories.
    [DataField(required: true)]
    public List<string> Categories = new();
}
