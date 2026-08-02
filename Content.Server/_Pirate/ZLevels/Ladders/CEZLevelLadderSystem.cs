using System.Numerics;
using Content.Server.Popups;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Ladders.Components;
using Content.Shared.DoAfter;
using Content.Shared._White.RadialSelector;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.ZLevels.Ladders;

public sealed class CEZLevelLadderSystem : EntitySystem
{
    private const string ClimbUpAction = "zlevel-ladder-climb-up";
    private const string ClimbDownAction = "zlevel-ladder-climb-down";
    private const float FallStartPosition = 0.99f;
    private const float FallStartVelocity = -0.1f;
    private static readonly TimeSpan ClimbCooldown = TimeSpan.FromSeconds(1);

    /// <summary>How often <see cref="Update"/> drains expired entries from <see cref="_nextClimb"/>.</summary>
    private static readonly TimeSpan NextClimbSweepInterval = TimeSpan.FromSeconds(30);

    private static readonly SpriteSpecifier UpIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Actions/misc.rsi"), "up");
    private static readonly SpriteSpecifier DownIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Actions/misc.rsi"), "down");

    private readonly Dictionary<EntityUid, TimeSpan> _nextClimb = new();
    private readonly HashSet<EntityUid> _activeClimbs = new();

    private TimeSpan _nextClimbSweep;

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelLadderComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CEZLevelLadderComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<CEZLevelLadderComponent, RadialSelectorSelectedMessage>(OnRadialSelected);
        SubscribeLocalEvent<CEZLevelLadderComponent, CEZLevelLadderClimbUpDoAfterEvent>(OnClimbUpDoAfter);
        SubscribeLocalEvent<CEZLevelLadderComponent, CEZLevelLadderClimbDownDoAfterEvent>(OnClimbDownDoAfter);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent ev)
    {
        _nextClimb.Remove(ev.Entity.Owner);
        _activeClimbs.Remove(ev.Entity.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextClimbSweep)
            return;

        _nextClimbSweep = now + NextClimbSweepInterval;

        // Cooldowns are otherwise only pruned when the same user calls IsOnCooldown or terminates,
        // so a user who climbs a ladder once and never returns would leak their entry forever.
        if (_nextClimb.Count == 0)
            return;

        List<EntityUid>? expired = null;
        foreach (var (uid, expiresAt) in _nextClimb)
        {
            if (expiresAt > now)
                continue;

            expired ??= new List<EntityUid>();
            expired.Add(uid);
        }

        if (expired is null)
            return;

        foreach (var uid in expired)
            _nextClimb.Remove(uid);
    }

    private void OnOpenAttempt(Entity<CEZLevelLadderComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (IsOnCooldown(args.User) || _activeClimbs.Contains(args.User))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("zlevel-ladder-popup-cooldown"), ent, args.User);
            return;
        }

        if (GetEntries(ent, args.User).Count > 0)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("zlevel-ladder-popup-no-route"), ent, args.User);
    }

    private void OnBeforeUiOpen(Entity<CEZLevelLadderComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        _ui.SetUiState(ent.Owner, RadialSelectorUiKey.Key, new RadialSelectorState(GetEntries(ent, args.User)));
    }

    private void OnRadialSelected(Entity<CEZLevelLadderComponent> ent, ref RadialSelectorSelectedMessage args)
    {
        switch (args.SelectedItem)
        {
            case ClimbUpAction:
                TryStartClimbUp(ent, args.Actor);
                break;
            case ClimbDownAction:
                TryStartClimbDown(ent, args.Actor);
                break;
        }

        _ui.CloseUi(ent.Owner, RadialSelectorUiKey.Key, args.Actor);
    }

    private List<RadialSelectorEntry> GetEntries(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        var entries = new List<RadialSelectorEntry>(2);

        if (IsOnCooldown(user) || _activeClimbs.Contains(user))
            return entries;

        if (CanClimbUp(ladder, user))
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ClimbUpAction,
                Icon = UpIcon,
            });
        }

        if (CanClimbDown(ladder, user))
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ClimbDownAction,
                Icon = DownIcon,
            });
        }

        return entries;
    }

    private bool TryStartClimbUp(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        if (IsOnCooldown(user) || _activeClimbs.Contains(user))
        {
            _popup.PopupEntity(Loc.GetString("zlevel-ladder-popup-cooldown"), ladder, user);
            return false;
        }

        if (!CanClimbUp(ladder, user))
            return false;

        return TryStartClimbDoAfter(ladder, user, new CEZLevelLadderClimbUpDoAfterEvent());
    }

    private bool TryClimbUp(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        if (!CanClimbUp(ladder, user))
            return false;

        var ladderWorldPos = _transform.GetWorldPosition(ladder);
        if (!_zLevels.TryMove(user, 1, targetWorldPositionOverride: ladderWorldPos, allowStairExitLanding: false))
            return false;

        _zLevels.NormalizeTransferredPullable(user, 1);
        return true;
    }

    private bool TryStartClimbDown(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        if (IsOnCooldown(user) || _activeClimbs.Contains(user))
        {
            _popup.PopupEntity(Loc.GetString("zlevel-ladder-popup-cooldown"), ladder, user);
            return false;
        }

        if (!CanClimbDown(ladder, user))
            return false;

        return TryStartClimbDoAfter(ladder, user, new CEZLevelLadderClimbDownDoAfterEvent());
    }

    private bool TryClimbDown(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        if (!CanClimbDown(ladder, user))
            return false;

        var ladderWorldPos = _transform.GetWorldPosition(ladder);
        var hasLadderBelow = HasLadderAtOffset(ladder, user, -1);

        if (!_zLevels.TryMove(user, -1, targetWorldPositionOverride: ladderWorldPos, allowStairExitLanding: false))
            return false;

        // Evaluate weightlessness on the destination layer — source gravity is irrelevant once we've descended.
        var isWeightlessAfter = IsWeightless(user);

        if (hasLadderBelow || isWeightlessAfter)
        {
            _zLevels.NormalizeTransferredPullable(user, -1);
            return true;
        }

        StartFallingFromUpperOpening(user);
        return true;
    }

    private bool TryStartClimbDoAfter(Entity<CEZLevelLadderComponent> ladder, EntityUid user, SimpleDoAfterEvent climbEvent)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ladder.Comp.ClimbDelay, climbEvent, ladder.Owner, target: ladder.Owner, used: ladder.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _activeClimbs.Add(user);
        StartCooldown(user);
        return true;
    }

    private void OnClimbUpDoAfter(Entity<CEZLevelLadderComponent> ent, ref CEZLevelLadderClimbUpDoAfterEvent args)
    {
        _activeClimbs.Remove(args.User);

        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryClimbUp(ent, args.User);
    }

    private void OnClimbDownDoAfter(Entity<CEZLevelLadderComponent> ent, ref CEZLevelLadderClimbDownDoAfterEvent args)
    {
        _activeClimbs.Remove(args.User);

        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryClimbDown(ent, args.User);
    }

    private bool IsOnCooldown(EntityUid user)
    {
        var now = _timing.CurTime;
        if (!_nextClimb.TryGetValue(user, out var nextClimb))
            return false;

        if (now < nextClimb)
            return true;

        _nextClimb.Remove(user);
        return false;
    }

    private void StartCooldown(EntityUid user)
    {
        _nextClimb[user] = _timing.CurTime + ClimbCooldown;
    }

    private bool CanClimbUp(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        var ladderWorldPos = _transform.GetWorldPosition(ladder);
        if (!_zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(user, 1, ladderWorldPos, out var aboveGridUid, out var aboveGrid))
            return false;

        if (_zLevels.HasLandingBlockerOnGridAtWorld(aboveGridUid, aboveGrid, ladderWorldPos))
            return false;

        if (!_zLevels.HasSupportAtWorldPositionOnGrid(aboveGridUid, aboveGrid, ladderWorldPos))
            return true;

        if (IsPassableSupportTileAtWorldPosition(ladder, aboveGridUid, aboveGrid, ladderWorldPos))
            return true;

        return HasAscentPassageAtWorldPosition(aboveGridUid, aboveGrid, ladderWorldPos);
    }

    private bool CanClimbDown(Entity<CEZLevelLadderComponent> ladder, EntityUid user)
    {
        var ladderWorldPos = _transform.GetWorldPosition(ladder);
        if (!_zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(user, -1, ladderWorldPos, out var belowGridUid, out var belowGrid))
            return false;

        return !_zLevels.HasLandingBlockerOnGridAtWorld(belowGridUid, belowGrid, ladderWorldPos);
    }

    private bool HasLadderAtOffset(Entity<CEZLevelLadderComponent> ladder, EntityUid user, int offset)
    {
        var ladderWorldPos = _transform.GetWorldPosition(ladder);
        if (!_zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(user, offset, ladderWorldPos, out var gridUid, out var grid))
            return false;

        return HasAnchoredComponentAtWorldPosition<CEZLevelLadderComponent>(gridUid, grid, ladderWorldPos);
    }

    private bool HasAscentPassageAtWorldPosition(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos)
    {
        return HasAnchoredComponentAtWorldPosition<CEZLevelAscentPassageComponent>(gridUid, grid, worldPos);
    }

    private bool IsPassableSupportTileAtWorldPosition(Entity<CEZLevelLadderComponent> ladder, EntityUid gridUid, MapGridComponent grid, Vector2 worldPos)
    {
        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var tileId = _turf.GetContentTileDefinition(tileRef).ID;
        if (ladder.Comp.PassableSupportTiles.Contains(tileId))
            return true;

        foreach (var prefix in ladder.Comp.PassableSupportTilePrefixes)
        {
            if (tileId.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool HasAnchoredComponentAtWorldPosition<T>(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos)
        where T : IComponent
    {
        var tile = _map.WorldToTile(gridUid, grid, worldPos);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<T>(uid.Value))
                return true;
        }

        return false;
    }

    private bool IsWeightless(EntityUid user)
    {
        return _gravity.IsWeightless(user); // Pirate: multiz - IsWeightless now reads cached GravityAffectedComponent, takes only the entity
    }

    private void StartFallingFromUpperOpening(EntityUid user)
    {
        if (!TryComp<CEZPhysicsComponent>(user, out var zPhysics))
            return;

        _zLevels.SetZPosition((user, zPhysics), FallStartPosition);
        if (zPhysics.Velocity > FallStartVelocity)
            _zLevels.SetZVelocity((user, zPhysics), FallStartVelocity);

        var fallEv = new CEZLevelFallMapEvent();
        RaiseLocalEvent(user, ref fallEv);
    }
}
