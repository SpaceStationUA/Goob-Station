// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Journey;

/// <summary>
///     One sector of the journey mode: a star system the station stays in until the next jump. The sector
///     decides which events may run while the ship is there, and which are held back.
/// </summary>
[Prototype]
public sealed partial class JourneySectorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Locale key of the sector's name.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    ///     Parallax prototype drawn behind the world in this sector.
    /// </summary>
    [DataField(required: true)]
    public string Parallax = string.Empty;

    /// <summary>
    ///     Event rules this sector never runs.
    /// </summary>
    [DataField]
    public List<ProtoId<EntityPrototype>> BlockedEvents = new();

    /// <summary>
    ///     The sector's majors: big threats that only run after the crew has been here for
    ///     <see cref="MajorDelayMinutes"/>, and not soon after a major anywhere else. Empty means a quiet
    ///     sector. An event listed here still has to pass the scheduler's own gates, so listing it only
    ///     delays it.
    /// </summary>
    [DataField]
    public List<ProtoId<EntityPrototype>> MajorEvents = new();

    /// <summary>
    ///     Minutes after arrival during which the sector's majors cannot run.
    /// </summary>
    [DataField]
    public float MajorDelayMinutes = 12f;

    /// <summary>
    ///     When not empty, the sector runs only these events. Empty means anything not blocked.
    /// </summary>
    [DataField]
    public List<ProtoId<EntityPrototype>> AllowedEvents = new();

    /// <summary>
    ///     Relative chance of being picked when the destination is chosen at random. Zero never picks it.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    ///     Whether the round may open in this sector. Trap sectors set this to false: the crew can still
    ///     jump there on purpose.
    /// </summary>
    [DataField]
    public bool AllowStart = true;
}
