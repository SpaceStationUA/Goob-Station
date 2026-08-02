// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Shared/_CMU14/ZLevels/Core/EntitySystems/CMUZLevelShootingSystem.cs.
// Adapted for lanos:
//   * SharedGunSystem notifies this system for each projectile so the visual offset can be
//     attached without requiring Shoot() to return a list.
//   * Inline LookUp-disable since lanos's viewer system doesn't expose TryDisableLookUp publicly.

using System.Numerics;
using Content.Shared._Pirate.Input;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Popups;
using SharedCCVars = Content.Shared.CCVar.CCVars;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.ZLevels.Shooting;

public sealed partial class CMUZLevelShootingSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    private float _crossZShotRange = 4f;
    private float _crossZOpeningSourceEdgeRangeTiles = 2f;

    /// <summary>
    /// Per-call visual-offset slot. Set by <see cref="BeginShotOffset"/> before <c>SharedGunSystem.Shoot</c>,
    /// consumed by <see cref="ApplyPendingProjectileVisualOffset"/> for every fired projectile, and
    /// cleared by <see cref="EndShotOffset"/> after.
    /// </summary>
    private Vector2 _pendingVisualOffset;
    private int _pendingShotDepth;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ItemUnwieldedEvent>(OnGunUnwielded);

        Subs.CVar(_config, SharedCCVars.CEZShootingRange, v => _crossZShotRange = MathF.Max(0f, v), true);
        Subs.CVar(_config, SharedCCVars.CEZShootingOpeningTileRange, v => _crossZOpeningSourceEdgeRangeTiles = MathF.Max(0f, v), true);

        CommandBinds.Builder
            .Bind(CEKeyFunctions.CEToggleShootDownZLevel,
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is { } user)
                            ToggleShootDown(user);
                    },
                    handle: false))
            .Register<CMUZLevelShootingSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CMUZLevelShootingSystem>();
    }

    private void OnGunUnwielded(Entity<GunComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (TryDisableShootDown(args.User) && !args.Force)
            PopupSelf(args.User, "ce-zlevel-shoot-down-disabled-unwield");
    }

    public void ApplyPendingProjectileVisualOffset(EntityUid projectile)
    {
        // Gate on depth, not barrelShift magnitude: a straight-up shot has zero shift but still
        // needs the client-side render compensation.
        if (_pendingShotDepth == 0)
            return;

        ApplyProjectileVisualOffset(projectile, _pendingVisualOffset, _pendingShotDepth);
    }

    /// <summary>Reserve the barrel-shift + shot depth applied to every projectile fired during the next Shoot call.</summary>
    public void BeginShotOffset(Vector2 barrelShift, int depth)
    {
        _pendingVisualOffset = barrelShift;
        _pendingShotDepth = depth;
    }

    public void EndShotOffset()
    {
        _pendingVisualOffset = Vector2.Zero;
        _pendingShotDepth = 0;
    }

    // --- ShootDown toggle -------------------------------------------------------------------

    private void ToggleShootDown(EntityUid user)
    {
        var enabled = IsShootDownEnabled(user);

        // Only require a ready gun when turning ON. Disabling must always be allowed, otherwise a
        // player who dropped/holstered their gun stays latched into ShootDown with no way to clear it.
        if (!enabled &&
            !TryGetReadyGun(user, "ce-zlevel-shoot-down-no-gun", "ce-zlevel-shoot-down-requires-wield"))
        {
            return;
        }

        var shootDown = !enabled;
        SetShootDown(user, shootDown);

        PopupSelf(user, shootDown ? "ce-zlevel-shoot-down-enabled" : "ce-zlevel-shoot-down-disabled");
    }

    private bool TryGetReadyGun(EntityUid user, string noGunMessage, string requiresWieldMessage)
    {
        if (!_gun.TryGetGun(user, out var gun)) // Pirate: multiz - TryGetGun now returns Entity<GunComponent>
        {
            PopupSelf(user, noGunMessage);
            return false;
        }

        if (!IsReadyGun(gun.Owner))
        {
            PopupSelf(user, requiresWieldMessage);
            return false;
        }

        return true;
    }

    private bool HasReadyGun(EntityUid user) =>
        _gun.TryGetGun(user, out var gun) && IsReadyGun(gun.Owner); // Pirate: multiz - TryGetGun now returns Entity<GunComponent>

    private bool IsReadyGun(EntityUid gunUid) =>
        !TryComp<WieldableComponent>(gunUid, out var wieldable) || wieldable.Wielded;

    private bool TryDisableShootDown(EntityUid user)
    {
        if (!IsShootDownEnabled(user))
            return false;

        SetShootDown(user, false);
        return true;
    }

    public bool IsShootDownEnabled(EntityUid user) =>
        TryComp<CMUZLevelShooterComponent>(user, out var shooter) && shooter.ShootDown;

    public void SetShootDown(EntityUid user, bool enabled)
    {
        CMUZLevelShooterComponent shooter;
        if (TryComp<CMUZLevelShooterComponent>(user, out var existing))
        {
            shooter = existing;
        }
        else
        {
            if (!enabled)
                return;

            shooter = EnsureComp<CMUZLevelShooterComponent>(user);
        }

        if (shooter.ShootDown == enabled)
            return;

        shooter.ShootDown = enabled;
        DirtyField(user, shooter, nameof(CMUZLevelShooterComponent.ShootDown));

        // ShootDown + LookUp are mutually exclusive — both modify the +/-1 routing.
        if (enabled)
            _zLevels.TryDisableLookUp(user);
    }

    // --- Shot redirection (called by SharedGunSystem) ---------------------------------------

    /// <summary>
    /// If the shooter has a Z-offset active (ShootDown=-1 or LookUp+gun=+1), rewrites
    /// <paramref name="fromCoordinates"/>/<paramref name="toCoordinates"/> to point at the
    /// target Z layer through a floor opening. Returns false (and shows a popup) if there's no
    /// adjacent layer or no opening along the shot line — caller should abort the shot.
    /// </summary>
    public bool TryAdjustShotCoordinates(
        EntityUid shooter,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out EntityCoordinates adjustedFromCoordinates,
        out EntityCoordinates adjustedToCoordinates,
        bool requireReadyGunForLookUp = true)
    {
        adjustedFromCoordinates = fromCoordinates;
        adjustedToCoordinates = toCoordinates;

        var offset = GetRequestedShotOffset(shooter, requireReadyGunForLookUp);
        if (offset == 0)
            return true;

        var shooterMap = Transform(shooter).MapUid;
        if (shooterMap == null ||
            !_zLevels.TryMapOffset((shooterMap.Value, null), offset, out var targetMap) ||
            !TryComp<MapComponent>(targetMap.Value.Owner, out var targetMapComp))
        {
            PopupSelf(shooter, offset > 0
                ? "ce-zlevel-shoot-up-no-level"
                : "ce-zlevel-shoot-down-no-level");
            return false;
        }

        var fromMap = _transform.ToMapCoordinates(fromCoordinates);
        var toMap = _transform.ToMapCoordinates(toCoordinates);
        var clampedTo = ClampCrossZShotTarget(fromMap.Position, toMap.Position);
        if (!_zLevels.TryFindZShotOpening(
                shooterMap.Value,
                targetMap.Value.Owner,
                offset,
                fromMap.Position,
                clampedTo,
                out _,
                preferOpeningAwayFromSource: true,
                maxSourceDistanceFromOpeningEdgeTiles: _crossZOpeningSourceEdgeRangeTiles))
        {
            PopupSelf(shooter, offset > 0
                ? "ce-zlevel-shoot-up-blocked-floor"
                : "ce-zlevel-shoot-down-blocked-floor");
            return false;
        }
        // The opening is only a gate (a hole must exist); we don't route through its center.
        // The adjacent-Z layer renders shifted by renderShift, so spawning and aiming the physics
        // in that shifted frame (physics = aim + renderShift) makes the bullet both render on the
        // aim line and collide where the player clicked — no cosmetic sprite offset.
        var renderShift = GetShooterRenderShift(shooter, offset);
        var projectileFrom = fromMap.Position + renderShift;
        var projectileTo = clampedTo + renderShift;

        var targetFrom = new MapCoordinates(projectileFrom, targetMapComp.MapId);
        var targetTo = new MapCoordinates(projectileTo, targetMapComp.MapId);

        adjustedFromCoordinates = _transform.ToCoordinates(targetFrom);
        adjustedToCoordinates = _transform.ToCoordinates(targetTo);
        return true;
    }

    /// <summary>
    /// World-space render-displacement of the adjacent-Z layer, matching <c>ScalingViewport.CEZLevels</c>:
    /// <c>gridWorldRotation.ToWorldVec() * ZLevelVisualOffset * offset</c>. Grid rotation is networked
    /// so client and server agree (prediction); a no-grid map gives the plain vertical shift.
    /// </summary>
    private Vector2 GetShooterRenderShift(EntityUid shooter, int offset)
    {
        var gridUid = Transform(shooter).GridUid;
        var gridRotation = gridUid is { } grid ? _transform.GetWorldRotation(grid) : Angle.Zero;
        return gridRotation.ToWorldVec() * CESharedZLevelsSystem.ZLevelVisualOffset * offset;
    }

    /// <summary>
    /// Outputs the eye-independent barrel-shift (projectile's target-layer spawn back to the source
    /// barrel) and the shot depth. Render-displacement compensation is added client-side from the
    /// live eye. Returns true for any cross-Z shot; barrelShift may be zero (firing straight up),
    /// so callers gate on depth.
    /// </summary>
    public bool TryGetProjectileVisualOffset(
        EntityUid shooter,
        EntityCoordinates sourceFromCoordinates,
        EntityCoordinates projectileFromCoordinates,
        out Vector2 barrelShift,
        out int depth,
        bool requireReadyGunForLookUp = true)
    {
        barrelShift = default;

        depth = GetRequestedShotOffset(shooter, requireReadyGunForLookUp);
        if (depth == 0)
            return false;

        var sourceFromMap = _transform.ToMapCoordinates(sourceFromCoordinates);
        var projectileFromMap = _transform.ToMapCoordinates(projectileFromCoordinates);
        if (sourceFromMap.MapId == MapId.Nullspace || projectileFromMap.MapId == MapId.Nullspace)
        {
            depth = 0;
            return false;
        }

        barrelShift = sourceFromMap.Position - projectileFromMap.Position;
        return true;
    }

    // --- Visual-offset attachment ------------------------------------------------------------

    public void ApplyProjectileVisualOffset(EntityUid projectile, Vector2 barrelShift, int depth)
    {
        if (depth == 0)
            return;

        // During client prediction, don't dirty server-owned entities. Use a predicted-only
        // component instead; the server will add the synced variant when it confirms the shot.
        if (_timing.InPrediction && !IsClientSide(projectile))
        {
            if (!TryComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(projectile, out var predicted))
            {
                predicted = new CMUZLevelPredictedProjectileVisualOffsetComponent { Offset = barrelShift, Depth = depth };
                AddComp(projectile, predicted);
                return;
            }

            predicted.Offset = barrelShift;
            predicted.Depth = depth;
            return;
        }

        if (!TryComp<CMUZLevelProjectileVisualOffsetComponent>(projectile, out var visual))
        {
            visual = new CMUZLevelProjectileVisualOffsetComponent { Offset = barrelShift, Depth = depth };
            AddComp(projectile, visual);
            Dirty(projectile, visual);
            return;
        }

        visual.Offset = barrelShift;
        visual.Depth = depth;
        Dirty(projectile, visual);
    }

    // --- Math helpers -----------------------------------------------------------------------

    private Vector2 ClampCrossZShotTarget(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();

        if (distance <= _crossZShotRange || distance <= 0.001f)
            return to;

        return from + delta / distance * _crossZShotRange;
    }

    private void PopupSelf(EntityUid user, string message) =>
        _popup.PopupClient(Loc.GetString(message), user, user, PopupType.SmallCaution);

    /// <summary>Resolves the shooter's current Z-offset request: -1 down, +1 up, 0 normal.</summary>
    private int GetRequestedShotOffset(EntityUid shooter, bool requireReadyGunForLookUp = false)
    {
        if (TryComp<CMUZLevelShooterComponent>(shooter, out var shooterComp) && shooterComp.ShootDown)
            return -1;

        if (TryComp<CEZLevelViewerComponent>(shooter, out var viewer) &&
            viewer.LookUp &&
            (!requireReadyGunForLookUp || HasReadyGun(shooter)))
        {
            return 1;
        }

        return 0;
    }
}
