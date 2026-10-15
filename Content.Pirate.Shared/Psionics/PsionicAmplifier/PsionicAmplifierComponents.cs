// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Psionics.PsionicAmplifier;

[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicActionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicAmplifierChipComponent : Component
{
    [DataField]
    public float CooldownMultiplier = 0.5f;

    [DataField(required: true)]
    public DamageSpecifier DispelDamage = default!;

    [DataField]
    public TimeSpan OverloadDuration = TimeSpan.FromSeconds(10);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PsionicAmplifiedComponent : Component
{
    [ViewVariables]
    public EntityUid Source;

    [DataField, AutoNetworkedField]
    public float CooldownMultiplier = 1f;

    [DataField]
    public DamageSpecifier? DispelDamage;

    [DataField]
    public TimeSpan OverloadDuration;
}
