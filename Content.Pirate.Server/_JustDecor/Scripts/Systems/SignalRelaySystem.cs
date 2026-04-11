using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class SignalRelaySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalRelayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SignalRelayComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, SignalRelayComponent comp, ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(uid, comp.SinkPort);

        if (!string.IsNullOrWhiteSpace(comp.OutputPort))
            _deviceLink.EnsureSourcePorts(uid, comp.OutputPort!);
    }

    private void OnSignalReceived(EntityUid uid, SignalRelayComponent comp, ref SignalReceivedEvent args)
    {
        if (args.Port != comp.SinkPort)
            return;

        if (comp.Sound != null)
            _audio.PlayPvs(comp.Sound, uid);

        if (!string.IsNullOrWhiteSpace(comp.OutputPort))
            _deviceLink.InvokePort(uid, comp.OutputPort!, args.Data);
    }
}
