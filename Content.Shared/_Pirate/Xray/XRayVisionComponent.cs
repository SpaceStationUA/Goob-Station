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

    // Pirate: meson vision - revealed tiles use the goggles shader.

    /// <summary>Opacity for unshaded revealed tiles.</summary>
    [DataField, AutoNetworkedField]
    public float TileAlpha = 0.2f;

    // Pirate: meson vision - bounds CPU occlusion checks.
    [DataField, AutoNetworkedField]
    public float Range = 12f;
}

public sealed partial class ToggleXRayVisionEvent : InstantActionEvent;
