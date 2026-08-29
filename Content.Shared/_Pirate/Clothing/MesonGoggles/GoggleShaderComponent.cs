// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Clothing.MesonGoggles;

/// <summary>Applies the configured full-screen shader while enabled.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GoggleShaderComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public string Shader = "Goggles";

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#5AB43CCC");
}

[ByRefEvent]
public readonly record struct GoggleShaderToggledEvent(bool Enabled);
