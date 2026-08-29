// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Xray;

/// <summary>
/// Enables the x-ray world overlay for the entity it is attached to, or for the wearer.
/// Shows tiles that are hidden behind walls with a scanline shader.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class XRayVisionComponent : Component
{
    /// <summary>
    /// Whether the overlay should be visible.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Whether wearing this entity should grant x-ray to the entity wearing it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RelayOverlay;

    /// <summary>
    /// The action proto that toggles the x-ray.
    /// </summary>
    /// <remarks>
    /// If null, no action is added.
    /// If <see cref="RelayOverlay"/> is true, it adds the action to the entity wearing this.
    /// Otherwise it adds the action to itself.
    /// </remarks>
    [DataField]
    public EntProtoId? Action;

    /// <summary>
    /// Reference to the action entity.
    /// </summary>
    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Whether tiles behind walls should be shown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShowTiles;

    // Pirate: upstream also carries TileOverlayColor/EntityOverlayColor/ScanlinesIntensity here, tinting
    // revealed tiles with their own shader on top of whatever full-screen effect the goggles apply. The hue
    // and the scanlines are dropped - this item reuses the T-ray goggles' full-screen shader (see
    // SharedXRayVisionSystem.SetEnabled) and revealed tiles should read as the same color as everything else.
    // Upstream's alpha is kept though, as TileAlpha below, because it is doing real work: see its docs.

    /// <summary>
    /// Opacity the revealed tiles are drawn at, as a neutral white modulate (no hue of its own).
    /// </summary>
    /// <remarks>
    /// This is the brightness knob, and it exists because revealed tiles are drawn <c>light_mode unshaded</c> -
    /// they sit outside FOV, so the lighting pass would otherwise black them out completely. Unshaded also
    /// means they skip the dimming everything else on screen receives, so at 1.0 they glare against a
    /// normally-lit floor. There is no correct value to use instead: the map's own
    /// <c>MapLightComponent.AmbientLightColor</c> defaults to black, and no per-pixel light data exists for
    /// tiles behind a wall. So this is a hand-tuned approximation of the ambient level - turn it down if
    /// revealed tiles glare, up if they are too faint to read. Upstream's equivalent was the alpha channel of
    /// its tile tint (0x30, ~0.19).
    /// </remarks>
    [DataField, AutoNetworkedField]
    public float TileAlpha = 0.2f;

    // Pirate: added on top of the upstream component. Upstream resolves occlusion per-pixel inside the shader
    // by sampling the engine's FOV shadow map, which needs RobustToolbox#6781 (IClydeViewport.FovRenderTarget)
    // - not present in our pinned engine 270.1.0. We resolve occlusion per-tile on the CPU instead, so it has
    // to be bounded by a radius to stay cheap. Upstream has no equivalent field.
    /// <summary>
    /// How far out, in tiles, hidden tiles are revealed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 12f;
}

public sealed partial class ToggleXRayVisionEvent : InstantActionEvent;
