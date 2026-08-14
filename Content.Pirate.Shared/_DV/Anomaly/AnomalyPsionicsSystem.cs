using Content.Shared._DV.CosmicCult;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Popups;

namespace Content.Shared._DV.Anomaly;

// Pirate: source patches SharedAnomalySystem partial, but DV psionics lives in Content.Pirate.Shared here.
public sealed class AnomalyPsionicsSystem : EntitySystem
{
    /// <summary>
    /// The stability a dispelled unstable anomaly is settled to. Must stay below the
    /// anomaly's growth threshold so it counts as stable.
    /// </summary>
    private const float AnomalyStabilizedStability = 0.3f;

    /// <summary>
    /// Health removed from a stable anomaly when it is dispelled (anomaly health is 0-1).
    /// </summary>
    private const float AnomalyDispelHealthDamage = 0.2f;

    [Dependency] private readonly SharedAnomalySystem _anomaly = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyComponent, DispelledEvent>(OnDispelled);
    }

    private void OnDispelled(Entity<AnomalyComponent> anomaly, ref DispelledEvent args)
    {
        if (HasComp<CosmicCultExamineComponent>(anomaly))
            return;

        // A dispel settles an unstable anomaly, but cracks the shell of a stable one.
        if (anomaly.Comp.Stability > anomaly.Comp.GrowthThreshold)
        {
            _anomaly.ChangeAnomalyStability(anomaly.Owner, AnomalyStabilizedStability - anomaly.Comp.Stability, anomaly.Comp);
            _popup.PopupPredicted(Loc.GetString("anomaly-dispel-stabilized"), anomaly.Owner, args.Dispeller, PopupType.Medium);
        }
        else
        {
            _anomaly.ChangeAnomalyHealth(anomaly.Owner, -AnomalyDispelHealthDamage, anomaly.Comp);
            _popup.PopupPredicted(Loc.GetString("anomaly-dispel-damaged"), anomaly.Owner, args.Dispeller, PopupType.MediumCaution);
        }

        args.Handled = true;
    }
}
