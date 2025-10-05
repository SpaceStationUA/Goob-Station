using Robust.Shared.Random;
using Content.Server.Abilities.Psionics;
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Psionics;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Mobs.Systems;
using Content.Shared.Psionics.Glimmer;
using Content.Shared.Zombies;
using Content.Pirate.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Body.Components;
using Content.Shared.Mind;

namespace Content.Pirate.Server.StationEvents.Events;

internal sealed class NoosphericStormRule : StationEventSystem<NoosphericStormRuleComponent>
{
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilitiesSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    protected override void Started(EntityUid uid, NoosphericStormRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> psionicList = new();
        List<EntityUid> nonPsionicList = new();


        // Get all alive humans with minds
        var allHumans = _mindSystem.GetAliveHumans();

        foreach (var human in allHumans)
        {
            var entity = new EntityUid(human.Owner.Id);

            if (!_mobStateSystem.IsAlive(entity) || HasComp<PsionicInsulationComponent>(entity))
                continue;

            if (!HasComp<BodyComponent>(entity))
                continue;

            if (HasComp<PsionicComponent>(entity))
                psionicList.Add(entity);
            else
                nonPsionicList.Add(entity);
        }
        // Give existing psionics new abilities
        if (psionicList.Count != 0)
        {
            RobustRandom.Shuffle(psionicList);
            var psionicsToAwaken = RobustRandom.Next(1, Math.Min(component.MaxAwaken, psionicList.Count));

            foreach (var target in psionicList)
            {
                _psionicAbilitiesSystem.AddRandomPsionicPower(target);
                if (psionicsToAwaken-- == 0)
                    break;
            }
        }

        // Give non-psionics psionic abilities
        if (nonPsionicList.Count != 0)
        {
            RobustRandom.Shuffle(nonPsionicList);
            var newPsionicsToAwaken = RobustRandom.Next(1, Math.Min(component.MaxAwaken, nonPsionicList.Count));

            foreach (var target in nonPsionicList)
            {
                _psionicAbilitiesSystem.AddRandomPsionicPower(target);
                if (newPsionicsToAwaken-- == 0)
                    break;
            }
        }

        // Increase glimmer
        var baseGlimmerAdd = _robustRandom.Next(component.BaseGlimmerAddMin, component.BaseGlimmerAddMax);
        _glimmerSystem.DeltaGlimmerInput(baseGlimmerAdd);
    }
}
