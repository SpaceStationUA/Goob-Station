using Content.Shared._DV.CosmicCult;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Robust.Shared.Random;

namespace Content.Shared._DV.Anomaly;

// Pirate: source patches SharedAnomalySystem partial, but DV psionics lives in Content.Pirate.Shared here.
public sealed class AnomalyPsionicsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAnomalySystem _anomaly = default!;
    [Dependency] private readonly SharedDispelPowerSystem _dispel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyComponent, DispelledEvent>(OnDispelled);
    }

    private void OnDispelled(Entity<AnomalyComponent> anomaly, ref DispelledEvent args)
    {
        if (HasComp<CosmicCultExamineComponent>(anomaly))
            return;

        _dispel.DealDispelDamage(anomaly.Owner, dispeller: args.Dispeller);
        _anomaly.ChangeAnomalyHealth(anomaly.Owner, -_random.NextFloat(0.4f, 0.8f), anomaly.Comp);
        args.Handled = true;
    }
}
