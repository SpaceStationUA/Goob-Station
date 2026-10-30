using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Light.Components;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Genetics.Light;

/// <summary>
/// Gives a point light random radius, energy and color when added to an entity with a <see cref="SharedPointLightComponent"/>.
/// Requires PointLightComponent to work properly.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RandomPointLightComponent : Component
{
    /// <summary>
    /// Maximum value for the random radius
    /// </summary>
    [DataField]
    public float MaxRadius = 6;

    /// <summary>
    /// Minimum value for the random radius
    /// </summary>
    [DataField]
    public float MinRadius = 3;

    /// <summary>
    /// Maximum value for the random energy
    /// </summary>
    [DataField]
    public float MaxEnergy = 5;

    /// <summary>
    /// Minimum value for the random energy
    /// </summary>
    [DataField]
    public float MinEnergy = 1;
}
