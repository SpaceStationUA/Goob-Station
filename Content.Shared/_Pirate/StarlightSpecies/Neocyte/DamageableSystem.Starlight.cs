namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    /// <summary>
    /// Pirate: body-wide healing has already applied the species penalty before being split across limbs.
    /// Direct limb treatments still need that penalty, including when they ignore damage resistances.
    /// </summary>
    internal bool IsApplyingBodyDamage(EntityUid body) => _complexDamageInProgress.Contains(body);
}
