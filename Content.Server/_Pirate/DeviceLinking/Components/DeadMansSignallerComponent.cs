// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.DeviceLinking.Components;

[RegisterComponent]
public sealed partial class DeadMansSignallerComponent : Component
{
    [DataField]
    public ProtoId<SourcePortPrototype> Port = "Pressed";
}
