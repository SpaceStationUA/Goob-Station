using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.StarlightSpecies.Movement;

/// <summary>Scales buffs and debuffs around the neutral multiplier of one, as in Starlight.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MovementSpeedModifierScaleComponent : Component
{
    [DataField]
    public float MovementSpeedScale = 1f;
}
