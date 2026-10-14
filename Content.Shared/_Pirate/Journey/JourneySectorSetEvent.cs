// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Journey;

/// <summary>
///     Server to client: the station is under this sector's sky now. A silent fade with no warp effects.
/// </summary>
/// <remarks>
///     The jump event is only sent when the ship moves, so it cannot tell a client where the ship already
///     is: round start and late joins need this one.
/// </remarks>
[Serializable]
[NetSerializable]
public sealed class JourneySectorSetEvent : EntityEventArgs
{
    /// <summary>Parallax prototype to show.</summary>
    public readonly string Parallax;

    public JourneySectorSetEvent(string parallax)
    {
        Parallax = parallax;
    }
}
