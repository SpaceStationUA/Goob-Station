// SPDX-License-Identifier: MIT
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

    [DataField, AutoNetworkedField]
    public Color TileOverlayColor = new(1f, 1f, 1f, 0.2f);

    [DataField, AutoNetworkedField]
    public float ScanlinesIntensity;
}

public sealed partial class ToggleXRayVisionEvent : InstantActionEvent;
