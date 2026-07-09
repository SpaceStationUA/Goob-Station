using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Abilities.SonicBoom;

/// <summary>
/// Gives the entity an action to emit a sonic shockwave and fling nearby entities.
/// </summary>
[RegisterComponent]
public sealed partial class AbilitySonicBoomComponent : Component
{
    /// <summary>
    /// Radius of the sonic boom effect.
    /// </summary>
    [DataField]
    public float FlingRadius = 1.0f;

    /// <summary>
    /// The strength of the fling at the center of the boom.
    /// This scales linearly with the target's distance from the entity.
    /// </summary>
    [DataField]
    public float FlingStrength = 1.5f;

    /// <summary>
    /// Slowdown percentage during the casting of the sonic boom.
    /// </summary>
    [DataField]
    public float Slowdown = 0.5f;

    /// <summary>
    /// Slowdown duration during the casting of the sonic boom.
    /// </summary>
    [DataField]
    public TimeSpan SlowdownDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Sound effect to play when the sonic boom is performed.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Voice/Moth/moth_scream.ogg");

    /// <summary>
    /// Action prototype given to the entity with this component.
    /// </summary>
    [DataField]
    public EntProtoId ActionProto = "ActionSonicBoom";

    /// <summary>
    /// Shockwave prototype spawned when the action is used.
    /// </summary>
    [DataField]
    public EntProtoId ShockwaveProto = "EffectShockwaveSonicBoom";

    /// <summary>
    /// Stored action entity.
    /// </summary>
    [DataField]
    public EntityUid? Action;

    public override bool SendOnlyToOwner => true;
}
