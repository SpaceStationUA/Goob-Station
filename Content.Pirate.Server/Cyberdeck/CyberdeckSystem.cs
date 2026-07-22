// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Cyberdeck.Components;
using Content.Pirate.Shared.Cyberdeck;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.VendingMachines;
using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Chat;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Pirate.Server.Cyberdeck;

public sealed class CyberdeckSystem : SharedCyberdeckSystem
{
    [Dependency] private readonly AirAlarmSystem _airAlarm = default!;
    [Dependency] private readonly ApcSystem _apcSystem = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly VendingMachineSystem _vending = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirAlarmComponent, CyberdeckHackDeviceEvent>(OnAirAlarmHacked);
        SubscribeLocalEvent<ApcComponent, CyberdeckHackDeviceEvent>(OnApcHacked);
        SubscribeLocalEvent<BatteryComponent, CyberdeckHackDeviceEvent>(OnBatteryHacked);
        SubscribeLocalEvent<PoweredLightComponent, CyberdeckHackDeviceEvent>(OnLightHacked);
        SubscribeLocalEvent<PowerNetworkBatteryComponent, CyberdeckHackDeviceEvent>(OnPowerNetworkHacked);
        SubscribeLocalEvent<VendingMachineComponent, CyberdeckHackDeviceEvent>(OnVendingHacked);
        SubscribeLocalEvent<CyberdeckUserComponent, CyberdeckInfoAlertEvent>(OnUserAlertClicked);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CyberdeckSourceComponent, AutoRechargeComponent, LimitedChargesComponent>();
        while (query.MoveNext(out var uid, out var source, out _, out _))
            RefreshSourceAlert((uid, source));
    }

    private void OnBatteryHacked(Entity<BatteryComponent> ent, ref CyberdeckHackDeviceEvent args)
    {
        var mass = 60f;
        if (TryComp(ent.Owner, out PhysicsComponent? physics))
            mass = physics.FixturesMass;

        mass = Math.Min(mass, 1000f);
        var currentCharge = _battery.GetCharge(ent.AsNullable());
        var percentage = currentCharge / Math.Max(ent.Comp.MaxCharge, 1f);

        if (percentage < 0.05f)
        {
            _battery.SetCharge(ent.AsNullable(), 0f);
            return;
        }

        var radius = percentage * MathF.Sqrt(mass) / 2f;
        var duration = TimeSpan.FromSeconds(percentage * 10f);
        _emp.EmpPulse(Xform.GetMapCoordinates(ent.Owner), radius, currentCharge, duration);

        var message = Loc.GetString(
            "cyberdeck-battery-get-hacked",
            ("target", Identity.Entity(ent.Owner, EntityManager, args.User)));
        Popup.PopupEntity(message, ent.Owner, PopupType.Large);
    }

    private void OnAirAlarmHacked(Entity<AirAlarmComponent> ent, ref CyberdeckHackDeviceEvent args)
    {
        var address = TryComp(ent.Owner, out DeviceNetworkComponent? network)
            ? network.Address
            : string.Empty;

        _airAlarm.SetMode(ent.Owner, address, AirAlarmMode.Panic, false, ent.Comp);
        ent.Comp.AutoMode = false;
    }

    private void OnApcHacked(Entity<ApcComponent> ent, ref CyberdeckHackDeviceEvent args)
        => _apcSystem.ApcToggleBreaker(ent.Owner, ent.Comp);

    private void OnLightHacked(Entity<PoweredLightComponent> ent, ref CyberdeckHackDeviceEvent args)
        => args.Refund = !_light.TryDestroyBulb(ent.Owner, ent.Comp);

    private void OnPowerNetworkHacked(Entity<PowerNetworkBatteryComponent> ent, ref CyberdeckHackDeviceEvent args)
    {
        if (TryComp(ent.Owner, out ExplosiveComponent? explosive))
            _explosion.TriggerExplosive(ent.Owner, explosive, user: args.User);
    }

    private void OnVendingHacked(Entity<VendingMachineComponent> ent, ref CyberdeckHackDeviceEvent args)
        => _vending.EjectRandom(ent.Owner, true, true, ent.Comp);

    private void OnUserAlertClicked(Entity<CyberdeckUserComponent> ent, ref CyberdeckInfoAlertEvent args)
    {
        if (args.Handled
            || ent.Comp.ProviderEntity is not { } provider
            || !TryComp<ActorComponent>(ent, out var actor)
            || !TryComp(provider, out LimitedChargesComponent? charges)
            || !TryComp(provider, out AutoRechargeComponent? recharge))
            return;

        var amount = Charges.GetCurrentCharges((provider, charges, recharge));
        var rechargeTime = (int) recharge.RechargeDuration.TotalSeconds;
        var message = Loc.GetString(
            "cyberdeck-get-alert-info",
            ("chargesAmount", amount),
            ("rechargeTime", rechargeTime));

        _chat.ChatMessageToOne(
            ChatChannel.Emotes,
            message,
            message,
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel);
        args.Handled = true;
    }
}
