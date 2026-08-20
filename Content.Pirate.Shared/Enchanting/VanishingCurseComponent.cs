// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Enchanting;

/// <summary>
/// Marks an item to disappear after the mob carrying it dies.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VanishingCurseComponent : Component
{
    [DataField]
    public float Lifetime = 1f;

    [DataField]
    public float FadeOutTime = 180f;
}
