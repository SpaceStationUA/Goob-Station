// Pirate: Starlight doll shell, using native organ events.
using Content.Shared.Actions;
using Content.Shared.Damage;

namespace Content.Shared._Pirate.StarlightSpecies.Doll;

public sealed partial class SnapShellPieceEvent : InstantActionEvent
{

    /// <summary>
    /// Damage applied to user when used
    /// </summary>
    [DataField]
    public DamageSpecifier? SelfDamage;

    /// <summary>
    /// Should this require a free hand to work?
    /// </summary>
    [DataField]
    public bool RequiresFreeHand = false;

    /// <summary>
    /// Want to remove *every* shell piece?
    /// </summary>
    [DataField]
    public bool DeShell = false;
}
