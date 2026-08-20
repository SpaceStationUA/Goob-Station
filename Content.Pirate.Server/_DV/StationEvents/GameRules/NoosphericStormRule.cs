using System.Linq;
using Content.Server._DV.Psionics.Systems;
using Content.Server._DV.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._DV.Psionics.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Psionics.Glimmer;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._DV.StationEvents.GameRules;

internal sealed class NoosphericStormRule : StationEventSystem<NoosphericStormRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly PsionicSystem _psionic = default!;

    protected override void Started(EntityUid uid, NoosphericStormRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        Dictionary<EntityUid, PotentialPsionicComponent> validList = [];

        var query = EntityManager.EntityQueryEnumerator<PotentialPsionicComponent>();
        while (query.MoveNext(out var potPsionic, out var potPsionicComp))
        {
            if (!_mobStateSystem.IsAlive(potPsionic)
                || !HasComp<ActorComponent>(potPsionic) // Skip non-player entities.
                || HasComp<PsionicComponent>(potPsionic)) // Skip over already psionic entities.
                continue;

            if (!_psionic.CanBeTargeted(potPsionic)) // Skip over shielded entities.
                continue;

            validList.Add(potPsionic, potPsionicComp);
        }

        // Give some targets psionic abilities.
        var keyList = validList.Keys.ToList();
        _robustRandom.Shuffle(keyList);

        var toAwaken = _robustRandom.Next(component.MinAwaken, component.MaxAwaken + 1);
        var additional = _glimmerSystem.Glimmer / component.AdditionalAwokenPerGlimmer;
        toAwaken += (int) MathF.Round(additional, 0, MidpointRounding.ToZero);

        foreach (var target in keyList.TakeWhile(_ => toAwaken-- != 0))
        {
            // Players get the accept/deny panel; NPCs are awakened directly.
            _psionic.OfferPsionicPower((target, validList[target]));
        }

        // Increase glimmer.
        var baseGlimmerAdd = _robustRandom.Next(component.BaseGlimmerAddMin, component.BaseGlimmerAddMax + 1);
        //var glimmerSeverityMod = 1 + (component.GlimmerSeverityCoefficient * (GetSeverityModifier() - 1f));
        var glimmerAdded = baseGlimmerAdd; // Math.Round(baseGlimmerAdd * glimmerSeverityMod);

        _glimmerSystem.Glimmer += glimmerAdded;
    }
}
