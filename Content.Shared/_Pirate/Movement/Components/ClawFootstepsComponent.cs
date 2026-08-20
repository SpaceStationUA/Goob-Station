// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Movement.Components;

/// <summary>
/// Makes a barefoot mob step with claws instead of bare skin. Nothing here applies while the mob has
/// shoes on - the shoes decide the sound long before the tile gets a say.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ClawFootstepsComponent : Component
{
    /// <summary>
    /// Barefoot tile sounds to swap out, keyed by the sound collection the tile asked for. A tile
    /// whose barefoot collection is not listed here is left alone, which is what keeps carpet (and
    /// anything else with its own barestep collection) on the shared barefoot sound.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SoundCollectionPrototype>, SoundSpecifier> Replacements = new()
    {
        // Note that BarestepHard is the default for every tile that does not name a collection, so
        // this covers plating, asteroid, grass and snow as well as actual hard floors.
        { "BarestepHard", new SoundCollectionSpecifier("ClawstepHard") },
        { "BarestepWood", new SoundCollectionSpecifier("ClawstepWood") },
    };
}
