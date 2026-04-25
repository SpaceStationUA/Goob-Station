using Content.Shared.Actions;

namespace Content.Shared._Pirate.Weapons.Melee;

/// <summary>
/// Fired when the player activates the "call cursed katana" action from a <c>DarkShardComponent</c>.
/// Summons the katana into the performer's hand, or retracts it if already summoned.
/// </summary>
public sealed partial class CallCursedKatanaEvent : InstantActionEvent;
