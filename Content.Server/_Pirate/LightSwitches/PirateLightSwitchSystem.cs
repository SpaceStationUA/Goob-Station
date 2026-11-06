// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.DeviceLinking;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.DeviceLinking;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Pirate.LightSwitches;

public sealed class PirateLightSwitchSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DeviceLinkSystem _links = default!;
    [Dependency] private readonly PoweredLightSystem _lights = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;

    private static readonly SoundPathSpecifier ClickSound = new("/Audio/Machines/lightswitch.ogg");
    private readonly Queue<EntityUid> _pendingStartup = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(PowerNetSystem));
        SubscribeLocalEvent<PirateLightSwitchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PirateLightSwitchComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnMapInit(Entity<PirateLightSwitchComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.IsOn = !ent.Comp.StartsOff;
        UpdateAppearance(ent);

        if (ent.Comp.StartsOff)
            _pendingStartup.Enqueue(ent);
    }

    public override void Update(float frameTime)
    {
        // Run after linked devices finish map initialization.
        while (_pendingStartup.TryDequeue(out var uid))
        {
            if (TryComp<PirateLightSwitchComponent>(uid, out var switchComp)
                && !TerminatingOrDeleted(uid)
                && !switchComp.IsOn)
            {
                TurnOffLinkedDevices(uid);
            }
        }
    }

    private void TurnOffLinkedDevices(EntityUid uid)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return;

        var linkedDevices = source.LinkedPorts.Keys.ToArray();
        _links.InvokePort(uid, "Off", sourceComponent: source);

        // Also update lights wired only to Toggle.
        foreach (var device in linkedDevices)
        {
            if (TryComp<PoweredLightComponent>(device, out var light) && light.On && !TerminatingOrDeleted(device))
                _lights.SetState(device, false, light);
        }
    }

    private void OnActivate(Entity<PirateLightSwitchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!TryComp<ApcPowerReceiverComponent>(ent, out var power) || !power.Powered)
            return;

        if (!_delay.TryResetDelay(ent, checkDelayed: true))
            return;

        ent.Comp.IsOn = !ent.Comp.IsOn;
        UpdateAppearance(ent);
        _links.InvokePort(ent, ent.Comp.IsOn ? "On" : "Off");
        _links.InvokePort(ent, "Pressed");
        _audio.PlayPvs(ClickSound, ent);
    }

    private void UpdateAppearance(Entity<PirateLightSwitchComponent> ent)
    {
        _appearance.SetData(ent, SignalSwitchVisuals.State, ent.Comp.IsOn ? SignalSwitchState.On : SignalSwitchState.Off);
    }
}
