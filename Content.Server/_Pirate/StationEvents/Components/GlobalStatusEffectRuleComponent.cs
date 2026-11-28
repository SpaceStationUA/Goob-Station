// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.StationEvents.Components;

[RegisterComponent, Access(typeof(GlobalStatusEffectRule))]
public sealed partial class GlobalStatusEffectRuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField]
    public float? MinEffectDuration = null;

    [DataField]
    public float? MaxEffectDuration = null;

    [DataField]
    public LocId? Announcement = null;
}
