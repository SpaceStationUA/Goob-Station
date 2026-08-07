// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkiaTechnokineticPulseComponent : Component
{
    [DataField]
    public float Range = 1f;

    [DataField]
    public float EnergyConsumption = 20000f;

    [DataField]
    public TimeSpan DisableDuration = TimeSpan.FromSeconds(20f);

    [DataField]
    public EntProtoId ActionId = "ActionTechnokineticPulse";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class SkiaTechnokineticPulseActionEvent : InstantActionEvent;
