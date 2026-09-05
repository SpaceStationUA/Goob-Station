// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class BatteryAmmoProviderComponent
{
    /// <summary>
    /// Fractional base-cost shots retained for multi-magazine cost multipliers.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float ShotsFloat;

    [ViewVariables, AutoNetworkedField]
    public float CapacityFloat;
}
