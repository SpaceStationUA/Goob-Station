namespace Content.Shared._starcup.StatusEffectsNew;

/// <summary>
/// Applies the component to the entity when they have the pertinent status effect.
/// </summary>
[RegisterComponent]
public sealed partial class ComponentStatusEffectComponent : Component
{
    /// <summary>
    /// The name of the component to apply, e.g. "Muted" for MutedComponent.
    /// </summary>
    [DataField]
    public string Component = string.Empty;
}
