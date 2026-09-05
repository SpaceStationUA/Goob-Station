// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.TextScreen;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Screens;

/// <summary>
/// Event-driven signal screen handling. No polling or entity scan is required.
/// </summary>
public sealed class SignalScreenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _device = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalScreenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SignalScreenComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(Entity<SignalScreenComponent> ent, ref ComponentInit args)
    {
        _device.EnsureSinkPorts(ent.Owner, ent.Comp.TextPort);
    }

    private void OnSignalReceived(Entity<SignalScreenComponent> ent, ref SignalReceivedEvent args)
    {
        TrySetText(ent, args.Port, args.Data);
    }

    public bool TrySetText(
        Entity<SignalScreenComponent> ent,
        ProtoId<SinkPortPrototype> port,
        NetworkPayload? data)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextChange || port != ent.Comp.TextPort || data == null)
            return false;

        string text;
        if (data.TryGetValue<string>("logic_string", out var stringValue))
            text = stringValue;
        else if (data.TryGetValue<int>("logic_int", out var intValue))
            text = intValue.ToString();
        else if (data.TryGetValue<SignalState>(DeviceNetworkConstants.LogicState, out var state))
        {
            text = state switch
            {
                SignalState.High => "true",
                SignalState.Low => "false",
                _ => "pulse",
            };
        }
        else
        {
            return false;
        }

        ent.Comp.NextChange = now + ent.Comp.ChangeCooldown;
        _appearance.SetData(ent.Owner, TextScreenVisuals.ScreenText, text);
        return true;
    }
}
