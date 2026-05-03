using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.GameTicking.Components;
using Content.Pirate.Server.StationEvents.Components;
using Robust.Shared.Random;
using Content.Server.Psionics.Glimmer;
using Robust.Shared.Maths;
using Robust.Shared.Console;
using Content.Shared.Psionics.Glimmer;
using Robust.Shared.Prototypes;
using Content.Pirate.Server.StationEvents.Components;

namespace Content.Pirate.Server.StationEvents.Events;

public sealed class PsionicEventsSchedulerRule : GameRuleSystem<PsionicEventsSchedulerComponent>
{
    [Dependency] private readonly EventManagerSystem _eventManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PsionicEventsSchedulerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var scheduler, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            scheduler.EventClock -= frameTime;
            if (scheduler.EventClock <= 0)
            {
                var glimmerSystem = _entMan.EntitySysManager.GetEntitySystem<GlimmerSystem>();
                var currentGlimmer = glimmerSystem.GlimmerOutput;
                TriggerRandomPsionicEvent(scheduler, currentGlimmer);
                ResetTimer(scheduler, currentGlimmer);
            }
        }
    }

    private void TriggerRandomPsionicEvent(PsionicEventsSchedulerComponent component, double currentGlimmer)
    {
        if (component.PsionicEvents.Count <= 0)
            return;

        var validEvents = new List<string>();

        foreach (var eventId in component.PsionicEvents)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(eventId, out var prototype))
                continue;

            if (!prototype.TryGetComponent<GlimmerEventComponent>(out var glimmerEvent))
                continue;

            if (currentGlimmer >= glimmerEvent.MinimumGlimmer &&
                currentGlimmer <= glimmerEvent.MaximumGlimmer)
            {
                validEvents.Add(eventId);
            }
        }

        if (validEvents.Count == 0)
            return;

        var randomEvent = _random.Pick(validEvents);
        _eventManager.RunNamedEvent(randomEvent);
    }

    private void ResetTimer(PsionicEventsSchedulerComponent component, double currentGlimmer)
    {
        float glimmer = (float)currentGlimmer;
        float t = Math.Clamp(glimmer / 900f, 0f, 1f);

        float delayMin = MathHelper.Lerp(component.DelayMin, 60f, t);
        float delayMax = MathHelper.Lerp(component.DelayMax, 90f, t);

        component.EventClock = _random.NextFloat(delayMin, delayMax);
    }

    protected override void Started(EntityUid uid, PsionicEventsSchedulerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        var glimmerSystem = _entMan.EntitySysManager.GetEntitySystem<GlimmerSystem>();
        ResetTimer(component, glimmerSystem.GlimmerOutput);
    }
}
