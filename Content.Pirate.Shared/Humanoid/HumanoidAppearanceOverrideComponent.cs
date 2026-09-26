// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Humanoid;

/// <summary>
/// Reshapes a freshly randomized humanoid on map init, then removes itself. Meant for randomHumanoidSettings
/// (e.g. HECU) where a squad should look uniform: short hair, no beards, similar build.
/// </summary>
[RegisterComponent]
public sealed partial class HumanoidAppearanceOverrideComponent : Component
{
    /// <summary>
    /// If set, head hair is re-rolled from these marking IDs, keeping the rolled hair colour.
    /// Styles the species can't wear are skipped; with none left the humanoid ends up bald.
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype>? HairStyles;

    [DataField]
    public bool RemoveFacialHair;

    /// <summary>
    /// Height range in centimetres (x = min, y = max), converted through the species' average height.
    /// </summary>
    [DataField]
    public Vector2? HeightCm;

    /// <summary>
    /// Width multiplier range (x = min, y = max).
    /// </summary>
    [DataField]
    public Vector2? Width;
}
