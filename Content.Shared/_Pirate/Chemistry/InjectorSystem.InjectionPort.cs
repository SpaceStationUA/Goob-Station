// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Chemistry;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.IdentityManagement;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class InjectorSystem
{
    /// <summary>
    /// Pirate: some mobs (synths) have a bloodstream but nothing to inject into.
    /// Without this the injector runs the whole do-after and then fails with a generic transfer popup.
    /// Drawing is left alone, blood can still be drawn from them.
    /// </summary>
    /// <returns>True if the injection was refused and a popup was shown.</returns>
    private bool TryRefuseNoInjectionPort(Entity<InjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (!_prototypeManager.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode)
            || activeMode.Behavior is not (InjectorBehavior.Inject or InjectorBehavior.Dynamic)
            || !HasComp<BloodstreamComponent>(target)
            || _solutionContainer.TryGetInjectableSolution(target, out _, out _)
            || _solutionContainer.TryGetRefillableSolution(target, out _, out _))
            return false;

        LocId msg = target == user ? "injector-component-no-injection-port-self" : "injector-component-no-injection-port";
        if (TryComp<InjectionRefusalMessageComponent>(target, out var refusal))
            msg = target == user ? refusal.SelfMessage : refusal.Message;

        _popup.PopupClient(Loc.GetString(msg, ("target", Identity.Entity(target, EntityManager))), user, user);
        return true;
    }
}
