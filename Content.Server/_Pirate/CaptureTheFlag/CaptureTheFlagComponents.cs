// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Content.Shared._Pirate.CaptureTheFlag;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.CaptureTheFlag;

public enum CaptureTheFlagTeam : byte
{
    Red,
    Blue,
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagRuleComponent : Component
{
    [DataField] public int ScoreToWin = 3;
    [DataField] public TimeSpan MatchDuration = TimeSpan.FromMinutes(12);
    [DataField] public TimeSpan RespawnDelay = TimeSpan.FromSeconds(7);
    [DataField] public TimeSpan DroppedFlagDuration = TimeSpan.FromSeconds(25);
    [DataField] public TimeSpan RebalanceDelay = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan EmptyTeamRebalanceDelay = TimeSpan.FromSeconds(20);
    [DataField] public TimeSpan ReconnectGrace = TimeSpan.FromSeconds(60);
    [DataField] public List<EntProtoId> TierTwoGear = new();
    [DataField] public List<EntProtoId> TierThreeGear = new();

    public EntityUid Grid;
    public EntityUid Relay;
    public CaptureTheFlagRelayState RelayState;
    public bool Started;
    public bool Finished;
    public TimeSpan? EndAt;
    public bool HadOpponents;
    public TimeSpan? ForfeitAt;
    public CaptureTheFlagTeam? ForfeitWinner;
    public bool Abandoned;
    public bool AnnouncedLastMinute;
    public int Tier = 1;
    public CaptureTheFlagTeam? LargerTeam;
    public TimeSpan LargerSince;
    public TimeSpan? EmptyTeamSince;
    public readonly Dictionary<CaptureTheFlagTeam, EntityUid> Homes = new();
    internal readonly Dictionary<NetUserId, CaptureTheFlagPlayer> Players = new();
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagBaseComponent : Component
{
    [DataField(required: true)] public CaptureTheFlagTeam Team;

    public EntityUid Rule;
    public EntityUid Flag;
    public EntityUid Supply;
    public int Score;
    public int ReserveCharges;
    public readonly List<EntityUid> Spawns = new();
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagSpawnComponent : Component
{
    [DataField(required: true)] public CaptureTheFlagTeam Team;
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagSupplyComponent : Component
{
    [DataField(required: true)] public CaptureTheFlagTeam Team;

    public EntityUid Rule;
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagRelayComponent : Component
{
    public EntityUid Rule;
    public CaptureTheFlagTeam? Team;
    public CaptureTheFlagTeam? ClaimingTeam;
    public TimeSpan CaptureAt;
    public TimeSpan LastCheck;
    public TimeSpan ChargeProgress;
    public bool Producing;
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagFlagComponent : Component
{
    public EntityUid Home;
    public bool AtHome = true;
    public EntityUid? Carrier;
    public TimeSpan? ReturnAt;
}

[RegisterComponent, Access(typeof(CaptureTheFlagRuleSystem))]
public sealed partial class CaptureTheFlagBodyComponent : Component
{
    public EntityUid Rule;
    public NetUserId Player;
}

internal sealed class CaptureTheFlagPlayer(CaptureTheFlagTeam team, HumanoidCharacterProfile profile)
{
    public CaptureTheFlagTeam Team = team;
    public HumanoidCharacterProfile Profile = profile;
    public EntityUid Body;
    public TimeSpan? RespawnAt;
    public int ClaimedTier;
}
