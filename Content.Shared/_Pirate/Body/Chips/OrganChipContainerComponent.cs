// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Body.Chips;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganChipContainerComponent : Component
{
    public const string ContainerId = "organ_chips";

    [DataField]
    public int Limit = 3;

    [ViewVariables]
    public Container? Container;
}
