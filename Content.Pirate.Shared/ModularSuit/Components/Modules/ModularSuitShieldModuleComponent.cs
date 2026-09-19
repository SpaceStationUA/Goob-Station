// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mono.PersonalShield;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

/// <summary>
/// Pirate: ERT modsuits - grants the wearer a pool-based <see cref="PersonalShieldComponent"/>
/// while the module is active.
///
/// This is deliberately not a plain <c>ModularSuitModuleWearerEffect</c>: that strips the
/// component the instant the module is switched off, which skips the shield's collapse
/// animation. Instead the module clears the shield's Enabled flag and lets it wind down on
/// its own, and plays the spin-up/shutdown sounds a hardsuit shield gets from its ItemToggle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModularSuitShieldModuleComponent : Component
{
    [DataField]
    public PersonalShieldSettings Shield = new();

    [DataField]
    public Color ShieldColor = Color.FromHex("#3A7DCDF2");

    [DataField]
    public SoundSpecifier? ActivateSound = new SoundPathSpecifier("/Audio/Weapons/ebladeon.ogg");

    [DataField]
    public SoundSpecifier? DeactivateSound = new SoundPathSpecifier("/Audio/Weapons/ebladeoff.ogg");

    [ViewVariables]
    public EntityUid? Wearer;
}
