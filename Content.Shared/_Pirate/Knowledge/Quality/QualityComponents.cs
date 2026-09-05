// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge.Quality;

/// <summary>
/// Immutable result of a single crafting-quality roll and its input requirements.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class QualityComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<EntProtoId, int> LevelDeltas = new();

    [DataField, AutoNetworkedField]
    public int Quality;

    [DataField, AutoNetworkedField]
    public int QualityModifiers;

    [DataField, AutoNetworkedField]
    public ProtoId<QualityPrototype> QualityFactors = "BaseQuality";

    [DataField, AutoNetworkedField]
    public bool Applied;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class QualityOverrideComponent : Component
{
    [DataField, AutoNetworkedField]
    public float QualityOverride = 1f;
}
