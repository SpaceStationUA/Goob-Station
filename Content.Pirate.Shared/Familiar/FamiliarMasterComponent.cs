// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Familiar;

/// <summary>
/// Identifies the master served by a familiar.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(FamiliarSystem))]
[AutoGenerateComponentState]
public sealed partial class FamiliarMasterComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Master;

    [DataField, AutoNetworkedField]
    public string MasterName = string.Empty;
}
