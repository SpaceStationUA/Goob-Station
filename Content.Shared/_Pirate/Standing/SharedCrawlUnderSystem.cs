using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Content.Shared.CCVar;
using Content.Shared.Physics;
using Robust.Shared.Physics; 
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Shared.Standing;

public class SharedCrawlUnderSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleCrawlingUnder, InputCmdHandler.FromDelegate(HandleCrawlUnderRequest, handle: false))
            .Register<SharedCrawlUnderSystem>();

        SubscribeLocalEvent<StandingStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<StandingStateComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<StandingStateComponent, DownedEvent>(OnDowned);
    }

    private void HandleCrawlUnderRequest(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } uid ||
            !TryComp<StandingStateComponent>(uid, out var standingState))
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        var curTime = _timing.CurTime;
        if (curTime < standingState.LastCrawlToggleTime + standingState.CrawlToggleCooldown)
        {
            return;
        }

        if (standingState.Standing)
            return;

        var newState = !standingState.IsCrawlingUnder;

        if (newState && !_config.GetCVar(CCVars.CrawlUnderTables))
            return;

        standingState.LastCrawlToggleTime = curTime;

        standingState.IsCrawlingUnder = newState;
        Dirty(uid, standingState);
        
        UpdatePhysicsState(uid, standingState);

        if (_net.IsServer)
        {
            var msg = newState ? "Ви залізли під меблі" : "Ви вилізли з-під меблів";
            _popups.PopupEntity(msg, uid, uid);
        }

        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnDowned(Entity<StandingStateComponent> ent, ref DownedEvent args)
    {
        UpdatePhysicsState(ent, ent.Comp);
    }

    private void OnStood(Entity<StandingStateComponent> ent, ref StoodEvent args)
    {
        if (ent.Comp.IsCrawlingUnder)
        {
            ent.Comp.IsCrawlingUnder = false;
            Dirty(ent);
        }
        
        UpdatePhysicsState(ent, ent.Comp);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void UpdatePhysicsState(EntityUid uid, StandingStateComponent standing)
    {
        if (HasComp<WormComponent>(uid))
            return;

        if (!TryComp<FixturesComponent>(uid, out var fixtures) || !TryComp<PhysicsComponent>(uid, out var physics))
            return;

        var maskBits = (int) CollisionGroup.MidImpassable;
        bool canPass = !standing.Standing || standing.IsCrawlingUnder;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (!fixture.Hard)
                continue;

            int newMask = fixture.CollisionMask;

            if (canPass)
            {
                if ((newMask & maskBits) != 0)
                {
                    newMask &= ~maskBits;
                    
                    if (!standing.ChangedFixtures.Contains(id))
                        standing.ChangedFixtures.Add(id);
                }
            }
            else
            {
                if (standing.ChangedFixtures.Contains(id))
                {
                    newMask |= maskBits;
                    standing.ChangedFixtures.Remove(id);
                }
            }

            if (newMask != fixture.CollisionMask)
            {
                _physics.SetCollisionMask(uid, id, fixture, newMask, fixtures, physics);
            }
        }
    }

    private void OnRefreshMovementSpeed(Entity<StandingStateComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Standing && ent.Comp.IsCrawlingUnder)
        {
            args.ModifySpeed(ent.Comp.CrawlingUnderSpeedModifier, ent.Comp.CrawlingUnderSpeedModifier);
        }
    }
}
