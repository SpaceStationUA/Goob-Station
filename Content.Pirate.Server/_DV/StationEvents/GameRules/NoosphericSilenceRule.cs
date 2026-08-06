using System.Linq;
using Content.Server._DV.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;

namespace Content.Server._DV.StationEvents.GameRules;

/// <summary>
/// Mutes a random amount of psionics for a random amount of time.
/// </summary>
internal sealed class NoosphericSilenceRule : StationEventSystem<NoosphericSilenceRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedPsionicSystem _psionic = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;

    protected override void Started(EntityUid uid, NoosphericSilenceRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Collect all eligible psionics.
        var psionics = new List<EntityUid>();
        var query = EntityQueryEnumerator<PsionicComponent, MobStateComponent>();
        while (query.MoveNext(out var psion, out _, out var mobState))
        {
            if (!_mobStateSystem.IsAlive(psion, mobState))
                continue;

            if (!_psionic.CanBeTargeted(psion))
                continue;

            psionics.Add(psion);
        }

        if (psionics.Count == 0)
            return;

        // Mute a random amount of them for a random amount of time.
        _robustRandom.Shuffle(psionics);
        var toSilence = Math.Min(_robustRandom.Next(component.MinAffected, component.MaxAffected + 1), psionics.Count);
        var duration = _robustRandom.Next(component.MinDuration, component.MaxDuration);

        foreach (var psion in psionics.Take(toSilence))
        {
            // TODO Replace with statusEffectSystemNew when Upstream makes a muted prototype.
            _statusEffectsSystem.TryAddStatusEffect(psion,
                "Muted",
                duration,
                false,
                "Muted");
        }
    }
}
