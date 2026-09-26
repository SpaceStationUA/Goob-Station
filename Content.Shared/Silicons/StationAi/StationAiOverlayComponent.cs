// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Handles the static overlay for station AI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Shitmed Change - Starlight Abductors
public sealed partial class StationAiOverlayComponent : Component
{
    /// <summary>
    ///     Shitmed Change - Starlight Abductors: Whether the station AI overlay should be allowed to cross grids.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowCrossGrid;

    /// <summary>Whether AI-only machines are visible and usable outside normal camera coverage.</summary> // Pirate: syndicate remote monitoring
    [DataField, AutoNetworkedField] // Pirate: syndicate remote monitoring
    public bool AllowUnseenMachineAccess = true; // Pirate: syndicate remote monitoring

    /// <summary>Include Syndicate PDA camera coverage in this observation view.</summary> // Pirate: syndicate remote monitoring
    [DataField, AutoNetworkedField] // Pirate: syndicate remote monitoring
    public bool IncludeSyndicateCameras; // Pirate: syndicate remote monitoring
}
