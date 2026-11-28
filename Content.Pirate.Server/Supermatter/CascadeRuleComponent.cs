// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Destructible.Thresholds;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Supermatter;


/// <summary>
///     Gamerule that stars & ends the resonance cascade event
/// </summary>
[RegisterComponent, Access(typeof(CascadeRuleSystem))]
public sealed partial class CascadeRuleComponent : Component
{
    [DataField]
    public EntityUid? Origin;

    /// <summary>
    /// The current stage of the resonance cascade event
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ResonanceCascadeStage Stage;

    /// <summary>
    /// Time until the initial announcements are triggered
    /// </summary>
    [DataField]
    public TimeSpan DurationForInitialAnnouncements = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Time until the initial announcements are triggered
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilInitialAnnouncements;

    /// <summary>
    /// Time until the round is ended in seconds
    /// </summary>
    [DataField]
    public TimeSpan DurationToRoundEnd = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Time until the round ends
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilEndRound;

    /// <summary>
    /// Amount of singularities to spawn once the round ends
    /// </summary>
    [DataField]
    public MinMax MinMaxSinglarity = new(2, 4);

    /// <summary>
    /// Amount of crystal mass to spawn throughout the station
    /// </summary>
    [DataField]
    public MinMax MinMaxCrystalMassSpawn = new(3, 4);

    [DataField]
    public ProtoId<ContentTileDefinition> CrystalMassPlating = "PlatingCrystalMass";

    [DataField]
    public EntProtoId CrystalBulbPrototype = "CrystalBulbSpreader";

    [DataField]
    public EntProtoId SingularityPrototype = "Singularity";
}

[Serializable]
public enum ResonanceCascadeStage : sbyte
{
    Beginning = 0,
    BeginningNTAnnouncement = 1,
    BeginningEvacCancel = 2,
    MiddleNTAnnouncement = 3,
    End = 4,
}
