using Robust.Shared.GameStates;

namespace Content.Pirate.Server._DV.Psionics.Components;

/// <summary>
///     Pirate: Marks a mob summoned by a familiar psionic power, linking it back to its caster.
/// </summary>
[RegisterComponent]
public sealed partial class SummonedFamiliarComponent : Component
{
    /// <summary>The psionic who summoned this familiar.</summary>
    [DataField]
    public EntityUid Caster;

    /// <summary>
    /// Next scheduled leash check (staggered to avoid checking every familiar every tick).
    /// </summary>
    public TimeSpan NextLeashCheck;
}
