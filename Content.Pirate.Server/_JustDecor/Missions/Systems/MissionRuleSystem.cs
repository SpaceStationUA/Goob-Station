using System.Numerics;
using Content.Pirate.Shared._JustDecor.Missions.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Server._JustDecor.Missions.Systems;

public sealed class MissionRuleSystem : GameRuleSystem<MissionRuleComponent>
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Started(EntityUid uid, MissionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        SpawnForRule(args.RuleId);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MissionSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(EntityUid uid, MissionSpawnerComponent component, MapInitEvent args)
    {
        TrySpawn(uid, component);
    }

    private void SpawnForRule(string ruleId)
    {
        var query = EntityQueryEnumerator<MissionSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (spawner.Spawned)
                continue;

            if (!spawner.GameRules.Contains(ruleId))
                continue;

            Spawn(uid, spawner);
        }
    }

    private void TrySpawn(EntityUid uid, MissionSpawnerComponent component)
    {
        if (component.Spawned)
            return;

        if (component.GameRules.Count == 0)
        {
            Spawn(uid, component);
            return;
        }

        foreach (var rule in component.GameRules)
        {
            if (!_ticker.IsGameRuleActive(rule))
                continue;

            Spawn(uid, component);
            return;
        }
    }

    private void Spawn(EntityUid uid, MissionSpawnerComponent component)
    {
        if (component.Chance != 1.0f && !_random.Prob(component.Chance))
            return;

        if (component.Prototypes.Count == 0)
        {
            Log.Warning($"Prototype list in MissionSpawnerComponent is empty! Entity: {ToPrettyString(uid)}");
            return;
        }

        var coords = Transform(uid).Coordinates;

        if (component.SpawnAll)
        {
            foreach (var proto in component.Prototypes)
            {
                var offsetCoords = coords.Offset(RandomOffset(component.Offset));
                Spawn(proto, offsetCoords);
            }
        }
        else
        {
            var offsetCoords = coords.Offset(RandomOffset(component.Offset));
            Spawn(_random.Pick(component.Prototypes), offsetCoords);
        }

        component.Spawned = true;

        if (component.DeleteAfterSpawn && !TerminatingOrDeleted(uid))
            QueueDel(uid);
    }

    private Vector2 RandomOffset(float offset)
    {
        if (offset <= 0f)
            return Vector2.Zero;

        var x = _random.NextFloat(-offset, offset);
        var y = _random.NextFloat(-offset, offset);
        return new Vector2(x, y);
    }
}
