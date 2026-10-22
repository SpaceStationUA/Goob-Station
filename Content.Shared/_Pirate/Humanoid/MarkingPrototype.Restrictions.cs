using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype
{
    [DataField("playerRestriction")]
    public List<string>? PlayerRestrictions { get; private set; }
}
