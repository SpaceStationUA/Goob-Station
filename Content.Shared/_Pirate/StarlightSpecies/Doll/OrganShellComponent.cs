using Content.Shared.Damage;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.StarlightSpecies.Doll;

/// <summary>A detachable doll shell fragment with its original colors and additive protection.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganShellComponent : Component
{
    [DataField]
    public DamageModifierSet Modifiers = new();

    [DataField]
    public List<Marking> StoredMarkings = new();
}
