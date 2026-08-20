// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.StationEvents.GameRules;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.StationEvents.Components;

/// <summary>
/// Announces a fugitive and delivers their warrant after a delay.
/// </summary>
[RegisterComponent, Access(typeof(FugitiveRule))]
[AutoGenerateComponentPause]
public sealed partial class FugitiveRuleComponent : Component
{
    [DataField]
    public LocId Announcement = "station-event-fugitive-hunt-announcement";

    [DataField]
    public LocId Sender = "fugitive-announcement-GALPOL";

    [DataField]
    public Color Color = Color.Yellow;

    [DataField]
    public EntProtoId ReportPaper = "PaperFugitiveReport";

    [DataField]
    public TimeSpan AnnounceDelay = TimeSpan.FromMinutes(5);

    [DataField]
    public EntityUid? Station;

    [DataField]
    public string Report = string.Empty;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextAnnounce;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> CrimesDataset = "FugitiveCrimes";

    [DataField]
    public int MinCrimes = 2;

    [DataField]
    public int MaxCrimes = 7;

    [DataField]
    public int MinCounts = 1;

    [DataField]
    public int MaxCounts = 4;
}
