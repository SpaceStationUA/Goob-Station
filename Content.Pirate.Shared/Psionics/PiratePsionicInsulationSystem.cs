using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Inventory;

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

    private void OnSiliconRollAttempt(Entity<SiliconComponent> ent, ref PsionicRollAttemptEvent args)
    {
        args.CanRoll = false;
    }
}
