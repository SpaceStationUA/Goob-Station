namespace Content.Pirate.Server.EnergyDome;

/// <summary>
/// marker component that allows linking the dome generator with the dome itself
/// </summary>

[RegisterComponent, Access(typeof(EnergyDomeSystem))]
public sealed partial class EnergyDomeComponent : Component
{
    /// <summary>
    /// A linked generator that uses energy
    /// </summary>
    [DataField]
    public EntityUid? Generator;

    /// <summary>
    /// Projectiles fired by entities inside this dome pass through it without damaging it.
    /// </summary>
    [DataField]
    public bool AllowProjectilesFromInside;

    /// <summary>Radius represented by the sprite at scale 1.</summary>
    [DataField]
    public float SpriteReferenceRadius = 1f;
}
