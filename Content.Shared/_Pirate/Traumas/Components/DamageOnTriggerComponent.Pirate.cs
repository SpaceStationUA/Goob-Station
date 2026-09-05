// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Targeting;
using Content.Shared.Whitelist;

namespace Content.Shared.Trigger.Components.Effects;

// Pirate: target filtering and limb targeting used by ported Trauma traps.
public sealed partial class DamageOnTriggerComponent
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public TargetBodyPart? TargetPart;
}
