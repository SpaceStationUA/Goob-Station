// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Movement.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ClawFootstepsComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SoundCollectionPrototype>, SoundSpecifier> Replacements = new()
    {
        { "BarestepHard", new SoundCollectionSpecifier("ClawstepHard") },
        { "BarestepWood", new SoundCollectionSpecifier("ClawstepWood") },
    };
}
