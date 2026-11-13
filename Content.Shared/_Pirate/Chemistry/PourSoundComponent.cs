// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Shared._Pirate.Chemistry;

[RegisterComponent]
public sealed partial class PourSoundComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("PirateLiquidPour", AudioParams.Default.WithVolume(-3f).WithVariation(0.05f));
}
