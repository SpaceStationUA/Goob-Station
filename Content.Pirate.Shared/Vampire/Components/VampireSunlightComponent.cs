using Content.Shared.FixedPoint;

namespace Content.Pirate.Shared.Vampire.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireSunlightComponent : Component
{
    /// <summary>
    ///     How much heat damage is applied when the burn effect triggers
    /// </summary>
    [DataField]
    public FixedPoint2 BurnDamage = FixedPoint2.New(3);

    /// <summary>
    ///     Interval between exposure ticks while in space
    /// </summary>
    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(2f);

    /// <summary>
    ///     Blood cost per exposure tick while the vampire still has reserves
    /// </summary>
    [DataField]
    public int BloodDrainPerInterval = 10;

    /// <summary>
    ///     Chance to apply the burn/ignite effect while the vampire still has blood
    /// </summary>
    [DataField]
    public float BloodEffectChance = 0.1f;

    /// <summary>
    ///     Chance to apply the burn/ignite effect while the vampire has no blood
    /// </summary>
    [DataField]
    public float BloodlessEffectChance = 0.85f;

    /// <summary>
    ///     Fire stacks added when the vampire ignites
    /// </summary>
    [DataField]
    public float FireStacksOnIgnite = 2f;

    [DataField]
    public FixedPoint2 BloodlessBurnDamage = FixedPoint2.New(5);

    /// <summary>
    ///     How long a vampire can linger in space before they start taking damage
    /// </summary>
    [DataField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    ///     Minimum seconds between popup warnings to the player
    /// </summary>
    [DataField]
    public TimeSpan WarningPopupCooldown = TimeSpan.FromSeconds(5f);

    /// <summary>
    ///     Localization string displayed when the vampire starts burning
    /// </summary>
    [DataField]
    public LocId WarningPopup = "vampire-space-burn-warning";

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? TimeEnteredSpace;

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? NextDamageTime;

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan NextWarningPopup;
}
