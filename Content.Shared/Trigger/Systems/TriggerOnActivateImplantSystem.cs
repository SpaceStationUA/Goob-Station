using Content.Shared.Implants.Components;
using Content.Shared.Trigger.Components.Triggers;

namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerOnActivateImplantSystem : TriggerOnXSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnActivateImplantComponent, ActivateImplantEvent>(OnActivateImplant);
    }

    private void OnActivateImplant(Entity<TriggerOnActivateImplantComponent> ent, ref ActivateImplantEvent args)
    {
        // Pirate: failed effects must not start the cooldown or consume a limited charge.
        args.Handled = Trigger.Trigger(ent.Owner, args.Performer, ent.Comp.KeyOut);
    }
}
