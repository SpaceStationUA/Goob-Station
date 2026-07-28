// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Heretic.Lock;

public sealed partial class EventMirrorJaunt : InstantActionEvent
{
    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "MirrorJaunt";

    [DataField]
    public float LookupRange = 1f;
}
