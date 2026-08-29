// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Clothing.MesonGoggles;

/// <summary>
/// Applies a full-screen shader (tint + scanline distortion) to the wearer while <see cref="Enabled"/>.
/// Kept in sync with the wearer's <c>TrayScannerComponent.Enabled</c> state by SharedTrayScannerSystem.
/// </summary>
/// <remarks>
/// Raises <c>AfterAutoHandleStateEvent</c> because the client caches <see cref="Color"/> into its overlay at
/// refresh time rather than reading it per-frame, so a networked color change has to announce itself - see
/// GoggleShaderSystem.OnHandleState (client).
/// </remarks>
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

/// <summary>
/// Raised on the entity when its goggle shader enabled state is toggled.
/// </summary>
[ByRefEvent]
public readonly record struct GoggleShaderToggledEvent(bool Enabled);
