using Content.Pirate.Shared.Alchemy.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistNitricEssenceSystem : EntitySystem
{
    private const float MinimumIntervalSeconds = 0.1f;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AlchemistNitricEssenceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            var interval = MathF.Max(comp.Interval, MinimumIntervalSeconds);
            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(interval);
            var slowdownSpan = TimeSpan.FromSeconds(comp.SlowdownSeconds);

            if (_atmosphere.GetContainingMixture(uid, excite: true) is { } air)
                _atmosphere.AddHeat(air, comp.AtmosphereHeatDelta);
            foreach (var nearby in _lookup.GetEntitiesInRange(uid, comp.Radius, LookupFlags.Dynamic))
            {
                if (nearby == uid || !TryComp<MobStateComponent>(nearby, out _))
                    continue;

                _statusEffects.TryAddStatusEffectDuration(nearby, comp.SlowdownEffect, slowdownSpan);
                _temperature.ChangeHeat(nearby, comp.TemperatureDelta, true);
            }
        }
    }
}
