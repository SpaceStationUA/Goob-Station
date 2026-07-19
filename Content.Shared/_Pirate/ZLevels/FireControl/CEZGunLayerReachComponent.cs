namespace Content.Shared._Pirate.ZLevels.FireControl;

/// <summary>
/// Added to a fire-controllable gun to override its default cross-z-layer firing reach. When
/// absent, guns fall back to the system default (<c>1</c>: same layer plus one above and below).
/// </summary>
[RegisterComponent]
public sealed partial class CEZGunLayerReachComponent : Component
{
    /// <summary>
    /// Maximum delta between the gun's depth and the targeted layer's depth at which the gun is
    /// still allowed to fire. <c>0</c> locks the gun to its own deck; <c>1</c> allows one layer
    /// up and one down.
    /// </summary>
    [DataField]
    public int Reach = 1;
}
