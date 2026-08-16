// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Silicons;

/// <summary>
/// Tracks the cyborg hand that owns a set of reusable holographic cuffs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorgHandcuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? OwnerChassis;

    [DataField, AutoNetworkedField]
    public string? HandId;
}
