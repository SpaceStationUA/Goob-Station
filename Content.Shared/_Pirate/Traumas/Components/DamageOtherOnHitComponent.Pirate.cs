// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Shared.Damage.Components;

// Pirate: Trauma thrown-weapon damage modifiers and impact audio.
public sealed partial class DamageOtherOnHitComponent
{
    [DataField]
    public bool IncreaseOnly;

    [DataField]
    public SoundSpecifier? SoundHit;

    [DataField]
    public bool ForceSound;
}
