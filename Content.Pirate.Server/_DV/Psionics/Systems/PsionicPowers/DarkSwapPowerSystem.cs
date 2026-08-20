using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Physics;
using Content.Shared.Shadowkin;
using Robust.Server.GameObjects;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class DarkSwapPowerSystem : SharedDarkSwapPowerSystem
{
    [Dependency] private readonly PhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnPowerUsed(Entity<DarkSwapPowerComponent> psionic, ref DarkSwapPowerActionEvent args)
    {
        var performer = args.Performer;

        if (TryComp<EtherealComponent>(performer, out var ethereal))
        {
            if (_physics.GetEntitiesIntersectingBody(performer, (int) CollisionGroup.Impassable).Count > 0)
            {
                // Blocked while inside a solid object - don't consume the cooldown.
                args.Handled = false;
                Popup.PopupEntity(Loc.GetString("revenant-in-solid"), performer, performer);
                return;
            }

            SpawnAtPosition("ShadowKudzuDarkSwap", Transform(performer).Coordinates);
            SpawnAtPosition("EffectFlashShadowkinDarkSwapOff", Transform(performer).Coordinates);
            RemComp(performer, ethereal);
        }
        else
        {
            var newEthereal = EnsureComp<EtherealComponent>(performer);
            newEthereal.Darken = true;

            // While in the shadow realm the user is pacified. Remember whether we were the
            // ones to add the pacification, so we can safely undo it when the realm ends.
            newEthereal.RemovePacifiedOnEnd = !HasComp<PacifiedComponent>(performer);
            if (newEthereal.RemovePacifiedOnEnd)
                EnsureComp<PacifiedComponent>(performer);
            Dirty(performer, newEthereal);

            SpawnAtPosition("ShadowKudzuDarkSwap", Transform(performer).Coordinates);
            SpawnAtPosition("EffectFlashShadowkinDarkSwapOn", Transform(performer).Coordinates);
        }

        AfterPowerUsed(psionic, args.Performer);
    }

    /// <summary>
    /// If a DarkSwap user gets mindbroken while in the shadow realm, pull them back out.
    /// </summary>
    protected override void OnMindBroken(Entity<DarkSwapPowerComponent> psionic, ref PsionicMindBrokenEvent args)
    {
        base.OnMindBroken(psionic, ref args);

        // The power was successfully removed, so exit the shadow realm.
        if (!HasComp<DarkSwapPowerComponent>(psionic.Owner) && HasComp<EtherealComponent>(psionic.Owner))
            RemComp<EtherealComponent>(psionic.Owner);
    }
}
