namespace Content.Server.Atmos.Components;

public sealed partial class BarotraumaComponent
{
    /// <summary>At least one equipped item in each group must provide pressure protection.</summary>
    [DataField]
    public List<List<string>> AlternativeProtectionSlots = new();
}
