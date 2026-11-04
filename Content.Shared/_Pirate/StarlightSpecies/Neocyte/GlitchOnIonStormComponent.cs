// Pirate: ported from ss14Starlight/space-station-14.
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.StarlightSpecies.Neocyte;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GlitchOnIonStormComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan Duration = TimeSpan.FromSeconds(10);
    [DataField, AutoNetworkedField] public TimeSpan Ramp = TimeSpan.FromSeconds(1);
}
