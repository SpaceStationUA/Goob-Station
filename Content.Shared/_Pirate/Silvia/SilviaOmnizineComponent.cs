// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Silvia;

/// <summary>
/// Tracks the Omnizine available for Silvia's melee injector.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SilviaOmnizineComponent : Component
{
    [DataField]
    public string Solution = "melee";

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Omnizine";

    [DataField]
    public ProtoId<AlertPrototype> Alert = "SilviaOmnizine";

    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 Amount;
}
