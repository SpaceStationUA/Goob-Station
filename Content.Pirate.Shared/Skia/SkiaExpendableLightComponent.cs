// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Supplies server-side values for expendable lights whose point light is not network-synchronized.
/// </summary>
[RegisterComponent]
public sealed partial class SkiaExpendableLightComponent : Component
{
    [DataField(required: true)]
    public float LitRadius;

    [DataField(required: true)]
    public float LitEnergy;

    [DataField]
    public float FadeInDuration;
}
