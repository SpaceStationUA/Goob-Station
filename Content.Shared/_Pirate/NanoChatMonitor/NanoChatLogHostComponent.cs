// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.NanoChatMonitor;

[RegisterComponent, NetworkedComponent]
public sealed partial class NanoChatLogHostComponent : Component
{
    [DataField]
    public bool RequiresKey = true;

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Common";
}
