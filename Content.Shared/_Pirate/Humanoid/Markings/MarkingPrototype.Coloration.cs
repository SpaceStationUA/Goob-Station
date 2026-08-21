using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype
{
    [DataField("coloration")]
    public ProtoId<MarkingColorationPrototype>? Coloration { get; private set; }
}
