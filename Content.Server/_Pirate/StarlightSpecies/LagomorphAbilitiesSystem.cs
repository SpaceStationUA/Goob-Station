using Content.Server.Actions;
using Content.Shared._Pirate.StarlightSpecies.Lagomorph;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StarlightSpecies;

/// <summary>Starlight's two lagomorph boosts using Pirate's stamina and nutrition APIs.</summary>
public sealed class LagomorphAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZoomiesComponent, MapInitEvent>(OnZoomiesInit);
        SubscribeLocalEvent<ZoomiesComponent, ComponentShutdown>(OnZoomiesShutdown);
        SubscribeLocalEvent<ZoomiesComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<StaminaSurgeComponent, MapInitEvent>(OnSurgeInit);
        SubscribeLocalEvent<StaminaSurgeComponent, ComponentShutdown>(OnSurgeShutdown);
        SubscribeLocalEvent<StaminaSurgeComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);
        SubscribeLocalEvent<ZoomiesActionEvent>(OnZoomies);
        SubscribeLocalEvent<StaminaSurgeActionEvent>(OnSurge);
    }

    private void OnZoomiesInit(Entity<ZoomiesComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);

    private void OnSurgeInit(Entity<StaminaSurgeComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);

    private void OnZoomiesShutdown(Entity<ZoomiesComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Active = false;
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
        _alerts.ClearAlert(ent.Owner, ent.Comp.ZoomiesAlert);
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnSurgeShutdown(Entity<StaminaSurgeComponent> ent, ref ComponentShutdown args)
    {
        EndSurge(ent);
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnSpeed(EntityUid uid, ZoomiesComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        Entity<ZoomiesComponent> ent = (uid, component);
        if (ent.Comp.Active && _timing.CurTime < ent.Comp.EffectEndTime)
            args.ModifySpeed(ent.Comp.SpeedModifier ?? 1f);
    }

    private void OnStaminaDamage(Entity<StaminaSurgeComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (ent.Comp.Active && _timing.CurTime < ent.Comp.EffectEndTime)
            args.Value *= ent.Comp.StaminaResistModifier ?? 1f;
    }

    private void OnZoomies(ZoomiesActionEvent args)
    {
        if (args.Handled || !TryComp<ZoomiesComponent>(args.Performer, out var boost) || boost.Active)
            return;
        boost.Active = true;
        boost.EffectEndTime = _timing.CurTime + (boost.Duration ?? TimeSpan.Zero);
        Dirty(args.Performer, boost);
        _movement.RefreshMovementSpeedModifiers(args.Performer);
        _alerts.ShowAlert(args.Performer, boost.ZoomiesAlert);
        args.Handled = true;
    }

    private void OnSurge(StaminaSurgeActionEvent args)
    {
        if (args.Handled || !TryComp<StaminaSurgeComponent>(args.Performer, out var boost) || boost.Active ||
            !TryComp<StaminaComponent>(args.Performer, out var stamina))
            return;
        boost.Active = true;
        boost.EffectEndTime = _timing.CurTime + (boost.Duration ?? TimeSpan.Zero);
        stamina.Decay *= boost.StaminaRegenModifier ?? 1f;
        stamina.Cooldown *= boost.StaminaCooldownModifier ?? 1f;
        Dirty(args.Performer, boost);
        Dirty(args.Performer, stamina);
        _alerts.ShowAlert(args.Performer, boost.SurgeAlert);
        args.Handled = true;
    }

    private void EndSurge(Entity<StaminaSurgeComponent> ent)
    {
        if (!ent.Comp.Active)
            return;
        ent.Comp.Active = false;
        if (TryComp<StaminaComponent>(ent, out var stamina))
        {
            stamina.Decay /= ent.Comp.StaminaRegenModifier ?? 1f;
            stamina.Cooldown /= ent.Comp.StaminaCooldownModifier ?? 1f;
            Dirty(ent, stamina);
        }
        _alerts.ClearAlert(ent.Owner, ent.Comp.SurgeAlert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var boosts = EntityQueryEnumerator<ZoomiesComponent>();
        while (boosts.MoveNext(out var uid, out var zoomies))
        {
            if (zoomies.Active && _timing.CurTime >= zoomies.EffectEndTime)
            {
                zoomies.Active = false;
                _movement.RefreshMovementSpeedModifiers(uid);
                _alerts.ClearAlert(uid, zoomies.ZoomiesAlert);
            }
        }
        var surges = EntityQueryEnumerator<StaminaSurgeComponent>();
        while (surges.MoveNext(out var uid, out var surge))
        {
            if (surge.Active && _timing.CurTime >= surge.EffectEndTime)
                EndSurge((uid, surge));
            var zoom = TryComp<ZoomiesComponent>(uid, out var zoomies) && zoomies.Active;
            if (!surge.Active && !zoom)
                continue;
            var hungerMultiplier = (surge.Active ? surge.HungerDrain ?? 1f : 1f) * (zoom ? zoomies!.HungerDrain ?? 1f : 1f);
            var thirstMultiplier = (surge.Active ? surge.ThirstDrain ?? 1f : 1f) * (zoom ? zoomies!.ThirstDrain ?? 1f : 1f);
            // Starlight adds boosted hunger drain to normal decay, and replaces the normal thirst drain.
            if (TryComp<HungerComponent>(uid, out var hunger))
                _hunger.ModifyHunger(uid, -hungerMultiplier * hunger.ActualDecayRate * frameTime, hunger);
            if (TryComp<ThirstComponent>(uid, out var thirst))
                _thirst.ModifyThirst(uid, thirst, -(thirstMultiplier - 1f) * thirst.ActualDecayRate * frameTime);
        }
    }
}
