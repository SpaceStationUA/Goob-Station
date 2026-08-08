// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkiaScreechShockWaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WaveSpeed = 15.3f;

    [DataField, AutoNetworkedField]
    public float WaveStrength = 1f;

    [DataField, AutoNetworkedField]
    public float DownScale = 1.5f;
}
