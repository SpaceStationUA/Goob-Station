using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchMirrorCurseSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly TimeSpan RetargetDelay = TimeSpan.FromSeconds(0.4);

    private readonly Dictionary<EntityUid, EntityUid?> _previousTargets = new();
    private readonly Dictionary<EntityUid, EntityUid> _currentTargets = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextRetarget = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchMirrorCurseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WitchMirrorCurseComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WitchMirrorCurseComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<ActorComponent>(ent.Owner, out _)
            || !TryComp<EyeComponent>(ent.Owner, out var eye))
            return;

        _previousTargets[ent.Owner] = eye.Target;
        _nextRetarget[ent.Owner] = TimeSpan.Zero;
        TryUpdateTarget(ent, eye);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WitchMirrorCurseComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var comp, out var eye))
        {
            if (!TryComp<ActorComponent>(uid, out _))
                continue;

            if (_nextRetarget.TryGetValue(uid, out var next) && _timing.CurTime < next)
                continue;

            TryUpdateTarget((uid, comp), eye);
        }
    }

    private void OnShutdown(Entity<WitchMirrorCurseComponent> ent, ref ComponentShutdown args)
    {
        _previousTargets.TryGetValue(ent.Owner, out var previous);
        _previousTargets.Remove(ent.Owner);
        _currentTargets.Remove(ent.Owner);
        _nextRetarget.Remove(ent.Owner);

        if (TryComp<EyeComponent>(ent.Owner, out var eye))
            _eye.SetTarget(ent.Owner, previous, eye);
    }

    private void TryUpdateTarget(Entity<WitchMirrorCurseComponent> ent, EyeComponent eye)
    {
        _nextRetarget[ent.Owner] = _timing.CurTime + RetargetDelay;

        if (_currentTargets.TryGetValue(ent.Owner, out var current) && IsValidTarget(ent, current))
        {
            var currentView = GetViewTarget(current);
            if (eye.Target != currentView)
                _eye.SetTarget(ent.Owner, currentView, eye);

            return;
        }

        if (!TryFindTarget(ent, out var target))
        {
            _currentTargets.Remove(ent.Owner);
            _previousTargets.TryGetValue(ent.Owner, out var previous);
            _eye.SetTarget(ent.Owner, previous, eye);
            return;
        }

        _currentTargets[ent.Owner] = target;
        _eye.SetTarget(ent.Owner, GetViewTarget(target), eye);
    }

    private bool TryFindTarget(Entity<WitchMirrorCurseComponent> ent, out EntityUid target)
    {
        var candidates = new List<EntityUid>();
        foreach (var candidate in _lookup.GetEntitiesInRange(ent, ent.Comp.Range, LookupFlags.Dynamic))
        {
            if (!IsValidTarget(ent, candidate))
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count > 0)
        {
            target = _random.Pick(candidates);
            return true;
        }

        target = EntityUid.Invalid;
        return false;
    }

    private bool IsValidTarget(Entity<WitchMirrorCurseComponent> ent, EntityUid candidate)
    {
        if (candidate == ent.Owner || !TryComp<MobStateComponent>(candidate, out _))
            return false;

        if (!_mobState.IsAlive(candidate))
            return false;

        if (!_interaction.InRangeUnobstructed(candidate, ent.Owner, ent.Comp.Range))
            return false;

        return true;
    }

    private EntityUid GetViewTarget(EntityUid candidate)
    {
        return TryComp<EyeComponent>(candidate, out var eye) && eye.Target is { } target
            ? target
            : candidate;
    }

}
