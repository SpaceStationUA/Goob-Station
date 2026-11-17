// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.Crayon;

/// <summary>
/// A crayon that stamps its user's custom symbol instead of its decal while they have one picked.
/// Every crayon has it, so a symbol picked with one crayon draws with any other.
/// </summary>
[RegisterComponent]
public sealed partial class CrayonSymbolComponent : Component;

/// <summary>
/// The custom symbol a player picked in a crayon window. Like a stencil it stays with the player,
/// not the crayon, until they pick a stock decal in any crayon window.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CrayonStencilSystem))]
public sealed partial class CrayonStencilComponent : Component
{
    [DataField, AutoNetworkedField]
    public CrayonSymbol? Symbol;

    [DataField, AutoNetworkedField]
    public Guid? DrawingId;

    [DataField] public string? DrawingName;

    // Only the player's own crayon windows show it.
    public override bool SendOnlyToOwner => true;
}

/// <summary>
/// A custom symbol stamped on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CrayonStampComponent : Component
{
    [DataField, AutoNetworkedField]
    public CrayonSymbol Symbol = new();

    // The server supplies the character name on examine. The account is only used for moderation.
    [DataField] public string? ArtistName;
    [DataField] public NetUserId? Artist;
    [DataField] public string? DrawingName;
}

[Serializable, NetSerializable]
public sealed class CrayonSelectSymbolMessage(Guid id) : BoundUserInterfaceMessage
{
    public readonly Guid Id = id;
}
