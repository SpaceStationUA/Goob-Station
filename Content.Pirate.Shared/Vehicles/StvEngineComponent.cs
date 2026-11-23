// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Shared.Vehicles;

[RegisterComponent]
public sealed partial class StvEngineComponent : Component
{
    [DataField(required: true)] public float FuelPerSecond;
}
