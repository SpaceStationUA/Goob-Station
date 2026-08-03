using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared.Shadowkin;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This is solely for prediction.
/// </summary>
public abstract class SharedDarkSwapPowerSystem : BasePsionicPowerSystem<DarkSwapPowerComponent, DarkSwapPowerActionEvent>
{
    /// <summary>
    /// The generic psionic-use gate blocks ALL powers while the user is ethereal
    /// (see <see cref="SharedEtherealSystem"/>). DarkSwap is the only way to leave
    /// the shadow realm, so it must stay usable to toggle back out.
    /// All other psionic actions remain blocked while ethereal.
    /// NOTE: this intentionally bypasses other suppression (tinfoil, psionics-disabled)
    /// while ethereal so the user is never stuck in the shadow realm.
    /// </summary>
    protected override bool CanUsePower(Entity<DarkSwapPowerComponent> psionic)
        => HasComp<EtherealComponent>(psionic.Owner) || base.CanUsePower(psionic);
}
