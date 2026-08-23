using Content.Shared._Pirate.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Pirate.Movement;

public sealed class PiratePullDensitySystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<PullableComponent> _pullableQuery;
    private readonly Dictionary<EntityUid, Dictionary<string, float>> _originalDensities = new();

    public override void Initialize()
    {
        base.Initialize();

        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _pullableQuery = GetEntityQuery<PullableComponent>();

        SubscribeLocalEvent<PullableComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<PullableComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<PullableComponent, EntityTerminatingEvent>(OnPullableTerminating);
    }

    private void OnPullStarted(Entity<PullableComponent> ent, ref PullStartedMessage args)
    {
        if (args.PullerUid is not { Valid: true } puller ||
            !TryComp<PullStrengthComponent>(puller, out var strength))
            return;

        ApplyDensityReduction(ent, strength.DensityReduction);
    }

    private void OnPullStopped(Entity<PullableComponent> ent, ref PullStoppedMessage args)
    {
        RestoreDensity(ent);
    }

    private void OnPullableTerminating(EntityUid uid, PullableComponent component, EntityTerminatingEvent args)
    {
        _originalDensities.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_originalDensities.Count == 0)
            return;

        var trackedEntities = new List<EntityUid>(_originalDensities.Keys);
        foreach (var uid in trackedEntities)
        {
            if (TerminatingOrDeleted(uid))
            {
                _originalDensities.Remove(uid);
                continue;
            }

            if (!_pullableQuery.TryComp(uid, out var pullable) ||
                pullable.Puller is not { Valid: true } ||
                !_physicsQuery.TryComp(uid, out var physics) ||
                physics.BodyType == BodyType.Static)
            {
                RestoreDensity(uid);
            }
        }
    }

    private void RestoreDensity(EntityUid uid)
    {
        if (!_originalDensities.TryGetValue(uid, out var originalDensities) ||
            !_fixturesQuery.TryComp(uid, out var fixtures))
        {
            _originalDensities.Remove(uid);
            return;
        }

        foreach (var (fixtureId, originalDensity) in originalDensities)
        {
            if (fixtures.Fixtures.TryGetValue(fixtureId, out var fixture))
                _physics.SetDensity(uid, fixtureId, fixture, originalDensity, false, fixtures);
        }

        _originalDensities.Remove(uid);
    }

    private void ApplyDensityReduction(EntityUid uid, float reduction)
    {
        if (reduction <= 0f || !_fixturesQuery.TryComp(uid, out var fixtures) ||
            _originalDensities.ContainsKey(uid))
            return;

        reduction = Math.Min(reduction, 0.9f);
        var originalDensities = new Dictionary<string, float>();
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
            originalDensities[fixtureId] = fixture.Density;

        _originalDensities[uid] = originalDensities;

        var multiplier = 1f - reduction;
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (originalDensities.TryGetValue(fixtureId, out var originalDensity))
                _physics.SetDensity(uid, fixtureId, fixture, Math.Max(0f, originalDensity * multiplier), false, fixtures);
        }
    }
}
