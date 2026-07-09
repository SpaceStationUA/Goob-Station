using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;

namespace Content.Pirate.Shared.TerrorSpider;

[RegisterComponent]
public sealed partial class InjectOnCollideComponent : Component
{
    [DataField("reagents")]
    public List<ReagentQuantity> Reagents = [];

    [DataField("limit")]
    public float? ReagentLimit;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public EntityWhitelist? Whitelist;
}
