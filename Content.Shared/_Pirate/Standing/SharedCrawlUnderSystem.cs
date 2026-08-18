using Content.Shared.Input;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Movement.Components;
using Content.Shared.Rotation;
using Content.Shared.Stunnable;
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
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleCrawlingUnder, InputCmdHandler.FromDelegate(HandleCrawlUnderRequest, handle: false))
            .Register<SharedCrawlUnderSystem>();

        SubscribeLocalEvent<StandingStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<StandingStateComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<StandingStateComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<StandingStateComponent, MoveInputEvent>(OnMoveInput);
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
        SetRotationAnimation(ent.Owner, instant: false);
        UpdatePhysicsState(ent, ent.Comp);
    }

    private void OnMoveInput(Entity<StandingStateComponent> ent, ref MoveInputEvent args)
    {
        if (ent.Comp.Standing || !args.State ||
            HasComp<SleepingComponent>(ent.Owner) ||
            HasComp<StunnedComponent>(ent.Owner) ||
            _mobState.IsIncapacitated(ent.Owner) ||
            TryComp<BuckleComponent>(ent.Owner, out var buckle) && buckle.Buckled)
            return;

        if (!TryComp(ent.Owner, out RotationVisualsComponent? rotation))
            return;

        var angle = args.Dir switch
        {
            Direction.East => rotation.DefaultRotation,
            Direction.West => -rotation.DefaultRotation,
            _ => (Angle?) null
        };

        if (angle == null)
            return;

        _rotationVisuals.SetHorizontalAngle(ent.Owner, angle.Value);
        SetRotationAnimation(ent.Owner, instant: true);
        RefreshRotationAppearance(ent.Owner);
    }

    private void OnStood(Entity<StandingStateComponent> ent, ref StoodEvent args)
    {
        SetRotationAnimation(ent.Owner, instant: false);

        if (ent.Comp.IsCrawlingUnder)
        {
            ent.Comp.IsCrawlingUnder = false;
            Dirty(ent);
        }
        
        UpdatePhysicsState(ent, ent.Comp);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void SetRotationAnimation(EntityUid uid, bool instant)
    {
        if (!TryComp<RotationVisualsComponent>(uid, out var rotation))
            return;

        var animationTime = instant ? 0f : 0.125f;
        if (rotation.AnimationTime == animationTime)
            return;

        rotation.AnimationTime = animationTime;
        Dirty(uid, rotation);
    }

    private void RefreshRotationAppearance(EntityUid uid)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, RotationVisuals.RotationState, RotationState.Horizontal, appearance);
        _appearance.QueueUpdate(uid, appearance);
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
