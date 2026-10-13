// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Journey;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Journey;

/// <summary>
///     The journey rule entity: the sector the station is in, and the sectors the round is limited to.
/// </summary>
[RegisterComponent, Serializable]
public sealed partial class JourneyRuleComponent : Component
{
    /// <summary>
    ///     The sector the station is in. Set when the round starts and again on every jump.
    /// </summary>
    [DataField]
    public string Sector = string.Empty;

    /// <summary>
    ///     The round time when the station entered the current sector. Majors wait out a grace period from it.
    /// </summary>
    [DataField]
    public TimeSpan SectorEnteredAt;

    /// <summary>
    ///     Warp length in seconds.
    /// </summary>
    [DataField]
    public float WarpDuration = 60f;

    /// <summary>
    ///     The sector the ship is headed for. Its rules apply when the warp ends.
    /// </summary>
    [DataField]
    public string PendingSector = string.Empty;

    /// <summary>
    ///     The round time when <see cref="PendingSector"/> takes effect.
    /// </summary>
    [DataField]
    public TimeSpan ApplyAt = TimeSpan.Zero;

    /// <summary>
    ///     Seconds between the commit and the warp visuals. The entry sound plays at the commit.
    /// </summary>
    [DataField]
    public float WarpRunUpSeconds = 15f;

    /// <summary>
    ///     Sectors the round is limited to. Empty means every sector is allowed.
    /// </summary>
    [DataField]
    public List<ProtoId<JourneySectorPrototype>> AllowedSectors = new();
}
