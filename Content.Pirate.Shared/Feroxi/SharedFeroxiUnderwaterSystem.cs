using Content.Pirate.Shared.Fluids;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Feroxi;

/// <summary>
/// Handles the Feroxi underwater ability: diving while standing in water, the movement speed and
/// unarmed damage boosts while under, and automatically surfacing when leaving the water, being
/// knocked down, entering crit, or dying.
/// </summary>
public sealed class SharedFeroxiUnderwaterSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> FootstepSoundTag = "FootstepSound";

    private EntityQuery<FloorWaterComponent> _floorWaterQuery;

    public override void Initialize()
    {
        base.Initialize();

        // Without this the client only ticks Update during prediction, which made surfacing lag
        // behind the local player by a long way at swim speed.
        UpdatesOutsidePrediction = true;

        _floorWaterQuery = GetEntityQuery<FloorWaterComponent>();

        SubscribeLocalEvent<FeroxiUnderwaterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, MeleeHitEvent>(OnMeleeHit);
    }

    /// <summary>
    /// Water contact is tracked by tile rather than by physics contacts - the water tile's fixture is
    /// deliberately smaller than its tile, so EndCollideEvent fires well after you've actually left,
    /// which made surfacing feel badly delayed (worse at swim speed).
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FeroxiUnderwaterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var water = FindWater(uid);
            if (comp.WaterEntity == water)
                continue;

            comp.WaterEntity = water;
            Dirty(uid, comp);

            var ent = new Entity<FeroxiUnderwaterComponent>(uid, comp);
            if (water == null)
                SetUnderwater(ent, false);

            UpdateActions(ent);
        }
    }

    /// <summary>
    /// Returns the water tile entity on the same tile as this mob, if any.
    /// </summary>
    private EntityUid? FindWater(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var anchoredUid))
        {
            if (_floorWaterQuery.HasComp(anchoredUid.Value))
                return anchoredUid.Value;
        }

        return null;
    }

    private void OnShutdown(Entity<FeroxiUnderwaterComponent> ent, ref ComponentShutdown args)
    {
        SetAction(ent, ref ent.Comp.DiveActionEntity, ent.Comp.DiveAction, false);
        SetAction(ent, ref ent.Comp.SurfaceActionEntity, ent.Comp.SurfaceAction, false);
        _alerts.ClearAlert(ent.Owner, ent.Comp.UnderwaterAlert);

        // Don't leave a mob permanently silent if the component goes away mid-swim.
        if (ent.Comp.RemovedFootstepTag)
        {
            _tags.AddTag(ent.Owner, FootstepSoundTag);
            ent.Comp.RemovedFootstepTag = false;
        }
    }

    /// <summary>
    /// Shows exactly one of the two actions, or neither on dry land. Dive is offered while standing in
    /// water on foot; Surface replaces it while underwater, so the button always names what it will do.
    /// </summary>
    private void UpdateActions(Entity<FeroxiUnderwaterComponent> ent)
    {
        SetAction(ent, ref ent.Comp.DiveActionEntity, ent.Comp.DiveAction,
            ent.Comp.WaterEntity != null && !ent.Comp.IsUnderwater);

        SetAction(ent, ref ent.Comp.SurfaceActionEntity, ent.Comp.SurfaceAction, ent.Comp.IsUnderwater);
    }

    /// <summary>
    /// Adds or removes a single action. Actions are server-authoritative, hence the server-only guard.
    /// </summary>
    private void SetAction(Entity<FeroxiUnderwaterComponent> ent, ref EntityUid? actionEntity, EntProtoId action, bool present)
    {
        if (!_net.IsServer || present == (actionEntity != null))
            return;

        if (!TryComp(ent, out ActionsComponent? actionsComp))
            return;

        if (present)
        {
            _actions.AddAction(ent, ref actionEntity, action, component: actionsComp);
        }
        else
        {
            _actions.RemoveAction((ent.Owner, actionsComp), actionEntity);
            actionEntity = null;
        }

        Dirty(ent);
    }

    private void OnToggle(Entity<FeroxiUnderwaterComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsUnderwater)
        {
            args.Handled = SetUnderwater(ent, false);
            return;
        }

        if (ent.Comp.WaterEntity == null)
        {
            _popup.PopupClient(Loc.GetString("feroxi-underwater-no-water"), ent, ent);
            return;
        }

        args.Handled = SetUnderwater(ent, true);
    }

    private void OnMobStateChanged(Entity<FeroxiUnderwaterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            SetUnderwater(ent, false);
    }

    /// <summary>
    /// Anything that puts the mob on the floor surfaces them - stamina crit, stuns, knockdowns,
    /// non-lethal takedowns. Covers all of them in one place since they all end up prone.
    /// </summary>
    private void OnDowned(Entity<FeroxiUnderwaterComponent> ent, ref DownedEvent args)
    {
        SetUnderwater(ent, false);
    }

    private void OnRefreshMovementSpeed(Entity<FeroxiUnderwaterComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.IsUnderwater)
            args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    /// <summary>
    /// Lunging out of the water hits harder. This only fires for unarmed attacks: when a weapon is
    /// held the hit event is raised on the weapon entity instead of on the attacker.
    /// </summary>
    private void OnMeleeHit(Entity<FeroxiUnderwaterComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.IsUnderwater || !args.IsHit || args.HitEntities.Count == 0)
            return;

        // BonusDamage is added to the base damage before modifier sets are applied, so adding one
        // extra copy of the base damage is what gets the configured multiplier.
        args.BonusDamage += args.BaseDamage * (ent.Comp.UnarmedDamageModifier - 1f);
    }

    /// <summary>
    /// Attempts to change the underwater state. Diving requires currently standing in water.
    /// Surfacing is always allowed - used both by the toggle action and by the auto-surface conditions.
    /// </summary>
    public bool SetUnderwater(Entity<FeroxiUnderwaterComponent> ent, bool underwater)
    {
        if (ent.Comp.IsUnderwater == underwater)
            return false;

        if (underwater && ent.Comp.WaterEntity == null)
            return false;

        ent.Comp.IsUnderwater = underwater;
        Dirty(ent);

        UpdateActions(ent);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent.Owner);

        if (underwater)
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp.UnderwaterAlert);

            // Swimming shouldn't make footstep noise. Same approach as SlasherIncorporealSystem.
            if (_tags.HasTag(ent.Owner, FootstepSoundTag))
            {
                _tags.RemoveTag(ent.Owner, FootstepSoundTag);
                ent.Comp.RemovedFootstepTag = true;
            }
        }
        else
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.UnderwaterAlert);

            if (ent.Comp.RemovedFootstepTag)
            {
                _tags.AddTag(ent.Owner, FootstepSoundTag);
                ent.Comp.RemovedFootstepTag = false;
            }
        }

        return true;
    }
}
