// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRuleComponent : Component
{
    #region State management

    public SuspicionGameState GameState = SuspicionGameState.Preparing;

    /// <summary>
    /// Deadline of the current phase in synchronized game time. Zero while the round is over.
    /// </summary>
    public TimeSpan EndAt;

    public HashSet<int> AnnouncedTimeLeft = new();
    public string? Winner;
    public TimeSpan NextPlayerCheck;

    #endregion


    [DataField]
    public int PostRoundDuration = 30;

    /// <summary>
    /// The gear all players spawn with.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype> Gear = "SuspicionGear";

    [DataField]
    public EntProtoId UplinkImplant = "SusTraitorUplinkImplant";

    [DataField]
    public EntProtoId DetectiveImplant = "SusDetectiveUplinkImplant";


    [DataField]
    public EntProtoId TraitorRadio = "EncryptionKeySyndie";

    [DataField]
    public ProtoId<NpcFactionPrototype> TraitorFaction = "Syndicate";

    [DataField]
    public EntProtoId DetectiveRadio = "EncryptionKeySecurity";

    /// <summary>
    /// How much TC to give to traitors/detectives for their performance
    /// </summary>
    [DataField]
    public int AmountAddedPerKill = 1;

}

public enum SuspicionGameState
{
    /// <summary>
    /// The game is preparing to start. No roles have been assigned yet and new joining players will be spawned in.
    /// </summary>
    Preparing,

    /// <summary>
    /// The game is in progress. Roles have been assigned and players are hopefully killing each other. New joining players will be forced to spectate.
    /// </summary>
    InProgress,

    /// <summary>
    /// The game has ended. The summary is being displayed and players are waiting for the round to restart.
    /// </summary>
    PostRound
}
