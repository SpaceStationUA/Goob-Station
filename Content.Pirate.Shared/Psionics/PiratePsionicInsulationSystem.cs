using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Inventory;
using Content.Shared.Shadowkin;

namespace Content.Pirate.Shared.Psionics;

public sealed class PiratePsionicInsulationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicallyInsulativeComponent, PsionicRollAttemptEvent>(OnInsulativeRollAttempt);
        SubscribeLocalEvent<PsionicallyInsulativeComponent, InventoryRelayedEvent<PsionicRollAttemptEvent>>(OnInsulativeRollAttemptRelayed);
        SubscribeLocalEvent<PsionicallyInsulativeComponent, PsionicPowerUseAttemptEvent>(OnInsulativePowerUseAttempt);
        SubscribeLocalEvent<PsionicallyInsulativeComponent, TargetedByPsionicPowerEvent>(OnInsulativeTargeted);
        SubscribeLocalEvent<EtherealComponent, TargetedByPsionicPowerEvent>(OnEtherealTargeted);
        SubscribeLocalEvent<SiliconComponent, PsionicRollAttemptEvent>(OnSiliconRollAttempt);
    }

    private void OnInsulativeRollAttempt(Entity<PsionicallyInsulativeComponent> ent, ref PsionicRollAttemptEvent args)
    {
        args.CanRoll &= !ent.Comp.ShieldsFromPsionics;
    }

    private void OnInsulativeRollAttemptRelayed(Entity<PsionicallyInsulativeComponent> ent, ref InventoryRelayedEvent<PsionicRollAttemptEvent> args)
    {
        args.Args.CanRoll &= !ent.Comp.ShieldsFromPsionics;
    }

    private void OnInsulativePowerUseAttempt(Entity<PsionicallyInsulativeComponent> ent, ref PsionicPowerUseAttemptEvent args)
    {
        args.CanUsePower &= ent.Comp.AllowsPsionicUsage;
    }

    private void OnInsulativeTargeted(Entity<PsionicallyInsulativeComponent> ent, ref TargetedByPsionicPowerEvent args)
    {
        args.IsShielded |= ent.Comp.ShieldsFromPsionics;
    }

    private void OnEtherealTargeted(Entity<EtherealComponent> ent, ref TargetedByPsionicPowerEvent args)
    {
        // While in the shadow realm via DarkSwap, the user is psionically insulated:
        // all psionic interactions with them (healing, zaps, detection, mindswap, etc.) are blocked.
        if (HasComp<DarkSwapPowerComponent>(ent.Owner))
            args.IsShielded = true;
    }

    private void OnSiliconRollAttempt(Entity<SiliconComponent> ent, ref PsionicRollAttemptEvent args)
    {
        args.CanRoll = false;
    }
}
