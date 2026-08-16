using Content.Server.Power.EntitySystems;
using Content.Shared.Anomaly.Components;
using Content.Shared.Psionics.Glimmer;

namespace Content.Server.Psionics.Glimmer;

/// <summary>
/// Handles structures which add/subtract glimmer.
/// </summary>
public sealed class GlimmerStructuresSystem : EntitySystem
{
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GlimmerSourceComponent, AnomalyPulseEvent>(OnAnomalyPulse);
        SubscribeLocalEvent<GlimmerSourceComponent, AnomalySupercriticalEvent>(OnAnomalySupercritical);
    }

    private void OnAnomalyPulse(EntityUid uid, GlimmerSourceComponent component, ref AnomalyPulseEvent args)
    {
        // Anomalies are meant to have GlimmerSource on them with the
        // active flag set to false, as they will be set to actively
        // generate glimmer when scanned to an anomaly vessel for
        // harvesting research points.
        //
        // It is not a bug that glimmer increases on pulse or
        // supercritical with an inactive glimmer source.
        //
        // However, this will need to be reworked if a distinction
        // needs to be made in the future. I suggest a GlimmerAnomaly
        // component.
        if (TryComp<AnomalyComponent>(uid, out var anomaly))
            _glimmerSystem.Glimmer += (int) (5f * anomaly.Severity);
    }

    private void OnAnomalySupercritical(EntityUid uid, GlimmerSourceComponent component, ref AnomalySupercriticalEvent args)
    {
        _glimmerSystem.Glimmer += 100;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Anomalies only generate glimmer while they are being harvested: an anomaly
        // vessel must be connected AND powered (i.e. actually producing research
        // points). Connection, disconnection, power loss and vessel destruction are
        // all picked up here automatically.
        foreach (var (anomaly, source) in EntityQuery<AnomalyComponent, GlimmerSourceComponent>())
        {
            source.Active = anomaly.ConnectedVessel is { } vessel
                && !Deleted(vessel)
                && _powerReceiverSystem.IsPowered(vessel);
        }

        foreach (var source in EntityQuery<GlimmerSourceComponent>())
        {
            if (!_powerReceiverSystem.IsPowered(source.Owner))
                continue;
            if (!source.Active)
                continue;
            source.Accumulator += frameTime;
            if (source.Accumulator > source.SecondsPerGlimmer)
            {
                source.Accumulator -= source.SecondsPerGlimmer;
                if (source.AddToGlimmer)
                {
                    _glimmerSystem.Glimmer++;
                }
                else
                {
                    _glimmerSystem.Glimmer--;
                }
            }
        }
    }
}
