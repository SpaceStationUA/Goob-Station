// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Xray;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class XRayVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool RelayOverlay;

    [DataField]
    public EntProtoId? Action;

    [DataField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool ShowTiles;

    // Pirate: upstream also carries TileOverlayColor/EntityOverlayColor/ScanlinesIntensity here, tinting
    // revealed tiles with their own shader on top of whatever full-screen effect the goggles apply. The hue
    // and the scanlines are dropped - this item reuses the T-ray goggles' full-screen shader (see
    // SharedXRayVisionSystem.SetEnabled) and revealed tiles should read as the same color as everything else.
    // Upstream's alpha is kept though, as TileAlpha below, because it is doing real work: see its docs.

    /// <summary>Opacity for unshaded revealed tiles.</summary>
    [DataField, AutoNetworkedField]
    public float TileAlpha = 0.2f;

    // Pirate: added on top of the upstream component. Upstream resolves occlusion per-pixel inside the shader
    // by sampling the engine's FOV shadow map, which needs RobustToolbox#6781 (IClydeViewport.FovRenderTarget)
    // - not present in our pinned engine 270.1.0. We resolve occlusion per-tile on the CPU instead, so it has
    // to be bounded by a radius to stay cheap. Upstream has no equivalent field.
    [DataField, AutoNetworkedField]
    public float Range = 12f;
}

public sealed partial class ToggleXRayVisionEvent : InstantActionEvent;
