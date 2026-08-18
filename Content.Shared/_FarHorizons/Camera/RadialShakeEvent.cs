/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Camera;

/// <summary>
/// Server -> client: rattle the receiving player's camera with a sustained radial
/// shake — per-frame random kicks tapering off over <see cref="Duration"/> — as
/// opposed to the single directional jolt of a plain KickCamera. Send it through a
/// grid/area <c>Filter</c> to shake everyone aboard. Handled by the client
/// <c>RadialShakeSystem</c>, which drives a client-only <c>RadialShakeComponent</c>
/// on the local player.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadialShakeEvent : EntityEventArgs
{
    /// <summary>How long the shake lasts, in seconds.</summary>
    public float Duration = 1.5f;

    /// <summary>
    /// Peak shake amplitude in tiles. Perceived shake scales linearly with this;
    /// the client hard-limits the eye offset to 1 tile (the recoil system's range).
    /// </summary>
    public float Amplitude = 1f;
}
