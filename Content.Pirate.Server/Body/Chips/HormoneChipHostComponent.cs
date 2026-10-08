// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Robust.Shared.Audio;

namespace Content.Pirate.Server.Body.Chips;

[RegisterComponent, Access(typeof(HormoneChipSystem))]
public sealed partial class HormoneChipHostComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, HormoneChipComponent> Applied = new();

    [ViewVariables]
    public int StamcritSources;

    [ViewVariables]
    public bool OwnsStamcrit;

    [ViewVariables]
    public int LightStepSources;

    [ViewVariables]
    public bool OwnsLightStep;

    [ViewVariables]
    public bool OwnsFootstepModifier;

    [ViewVariables]
    public SoundSpecifier? PreviousFootsteps;
}
