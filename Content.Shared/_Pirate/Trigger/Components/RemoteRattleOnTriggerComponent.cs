// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Radio;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Trigger.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteRattleOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Cybersun";

    [DataField]
    public Dictionary<MobState, LocId> Messages = new()
    {
        { MobState.Critical, "rattle-on-trigger-critical-message" },
        { MobState.Dead, "rattle-on-trigger-dead-message" },
    };

    [DataField]
    public bool ReportCoordinates;
}
