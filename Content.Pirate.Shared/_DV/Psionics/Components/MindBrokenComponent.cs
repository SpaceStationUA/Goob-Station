using Robust.Shared.GameStates;

namespace Content.Shared._DV.Psionics.Components;

/// <summary>
///     Marks an entity as permanently mindbroken: stripped of all psionic abilities and
///     psionic potential, and completely psionically insulated (cannot use, cannot be
///     targeted, cannot roll).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MindBrokenComponent : Component
{
    /// <summary>
    ///     Locale string shown instead of the default when examining this entity up close.
    ///     Populated from <see cref="PotentialPsionicComponent.MindbrokenExamineDesc"/> when the
    ///     entity becomes mindbroken, or set directly (e.g. on the Shadowkin species).
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? MindbrokenExamineDesc;

    /// <summary>
    ///     Whether <see cref="Content.Pirate.Shared.Blinking.BlinkingComponent"/> was disabled by
    ///     this mindbroken state (eyes don't blink), so it can be restored if the state is removed.
    /// </summary>
    [DataField]
    public bool BlinkingDisabled;
}
