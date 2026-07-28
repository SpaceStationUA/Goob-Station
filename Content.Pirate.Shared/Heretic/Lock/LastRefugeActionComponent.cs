// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Heretic.Lock;

[RegisterComponent, NetworkedComponent]
public sealed partial class LastRefugeActionComponent : Component
{
    [DataField]
    public float OtherMindsCheckRange = 5f;
}
