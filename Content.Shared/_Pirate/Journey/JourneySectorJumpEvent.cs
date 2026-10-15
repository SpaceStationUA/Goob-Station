// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Journey;

/// <summary>
///     Server to client: the station jumps to another sector now.
/// </summary>
[Serializable]
[NetSerializable]
public sealed class JourneySectorJumpEvent : EntityEventArgs
{
    /// <summary>Parallax prototype the ship arrives in.</summary>
    public readonly string Parallax;

    /// <summary>Warp length in seconds.</summary>
    public readonly float Duration;

    /// <summary>Seconds between this broadcast and the warp visuals. The entry sound plays now.</summary>
    public readonly float Delay;

    public JourneySectorJumpEvent(string parallax, float duration, float delay = 0f)
    {
        Parallax = parallax;
        Duration = duration;
        Delay = delay;
    }
}
