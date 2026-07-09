namespace Content.Pirate.Shared.TerrorSpider.Evolving;

[RegisterComponent]
public sealed partial class EvolveConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EvolveType? ConditionType;

    [DataField("count"), ViewVariables(VVAccess.ReadWrite)]
    public int Count;
}
