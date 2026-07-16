// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Nuclear.Turbine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GasTurbineBladeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int? Health;

    [DataField, AutoNetworkedField]
    public int? MaxHealth;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class GasTurbineStatorComponent : Component;
