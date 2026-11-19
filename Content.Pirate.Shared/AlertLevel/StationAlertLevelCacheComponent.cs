// SPDX-License-Identifier: AGPL-3.0-or-later
// Attribution: AdventureTimeSS14/space_station_ADT PR #3338 (MIT).

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.AlertLevel;

// Mirror the server-only station alert level to its owner for client prediction.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAlertLevelCacheComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public string CurrentLevel = string.Empty;
}
