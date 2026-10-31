// Pirate: ported from ss14Starlight/space-station-14.
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.StarlightSpecies.Neocyte;

/// <summary>
/// Carries a Neocyte's selected frame through polymorphs.
/// </summary>
[RegisterComponent]
public sealed partial class NeocyteFrameMemoryComponent : Component
{
    [ViewVariables]
    public EntProtoId? FramePrototype;
}
