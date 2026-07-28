using Content.Pirate.Shared.PAI;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Goobstation.Shared.Overlays;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.PAI;

public sealed class PAIProgramSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PAIToggleNightVisionEvent>(OnToggleNightVision);
        SubscribeLocalEvent<PAIToggleThermalVisionEvent>(OnToggleThermalVision);
        SubscribeLocalEvent<PAILightFlickerEvent>(OnLightFlicker);
        SubscribeLocalEvent<PAIHealthScanEvent>(OnHealthScan);
        SubscribeLocalEvent<PAIToggleFlashlightEvent>(OnToggleFlashlight);
        SubscribeLocalEvent<PAIToggleMedHudEvent>(OnToggleMedHud);
        SubscribeLocalEvent<PAISmokeEvent>(OnSmoke);
    }

    private void OnToggleNightVision(PAIToggleNightVisionEvent args)
    {
        var uid = args.Performer;
        if (!TryComp<NightVisionComponent>(uid, out var nv))
        {
            nv = AddComp<NightVisionComponent>(uid);
            nv.IsEquipment = false;
            nv.IsActive = true;
            Dirty(uid, nv);
            return;
        }

        nv.IsActive = !nv.IsActive;
        Dirty(uid, nv);
    }

    private void OnToggleThermalVision(PAIToggleThermalVisionEvent args)
    {
        var uid = args.Performer;
        if (!TryComp<ThermalVisionComponent>(uid, out var tv))
        {
            tv = AddComp<ThermalVisionComponent>(uid);
            tv.IsEquipment = false;
            tv.IsActive = true;
            Dirty(uid, tv);
            return;
        }

        tv.IsActive = !tv.IsActive;
        Dirty(uid, tv);
    }

    private void OnLightFlicker(PAILightFlickerEvent args)
    {
        var uid = args.Performer;
        var lightQuery = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var lightUid, out var light, out var xform))
        {
            if (!TryComp<TransformComponent>(uid, out var performerXform))
                continue;

            if (xform.MapID != performerXform.MapID)
                continue;

            var dist = (xform.WorldPosition - performerXform.WorldPosition).Length();
            if (dist > 7f)
                continue;

            EntityManager.System<SharedPoweredLightSystem>().SetState(lightUid, false, light);

            var lightUidCopy = lightUid;
            var lightComp = light;
            Timer.Spawn(TimeSpan.FromSeconds(5), () =>
            {
                if (EntityManager.TryGetComponent(lightUidCopy, out PoweredLightComponent? lightCompRef))
                    EntityManager.System<SharedPoweredLightSystem>().SetState(lightUidCopy, true, lightCompRef);
            });
        }
    }

    private void OnHealthScan(PAIHealthScanEvent args)
    {
        // handled by PAIHealthSystem
    }

    private void OnToggleFlashlight(PAIToggleFlashlightEvent args)
    {
        var uid = args.Performer;
        if (!TryComp<PointLightComponent>(uid, out var light))
            return;

        EntityManager.System<SharedPointLightSystem>().SetEnabled(uid, !light.Enabled, light);
    }

    private void OnToggleMedHud(PAIToggleMedHudEvent args)
    {
        var uid = args.Performer;
        if (HasComp<ShowHealthBarsComponent>(uid))
            RemComp<ShowHealthBarsComponent>(uid);
        else
            AddComp<ShowHealthBarsComponent>(uid);
    }

    private void OnSmoke(PAISmokeEvent args)
    {
        var uid = args.Performer;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/smoke.ogg"), uid);
        _popup.PopupClient(Loc.GetString("pai-smoke-start"), uid, uid);

        for (var i = 0; i < 5; i++)
        {
            var delay = i * 0.6f;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (!Exists(uid))
                    return;
                var xform = Transform(uid);
                var ent = Spawn("Smoke", xform.Coordinates);
                _smoke.StartSmoke(ent, new Solution(), 3f, 1);
            });
        }

        args.Handled = true;
    }
}
