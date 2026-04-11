using System.Collections.Generic;
using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Server.Chat.Systems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Chat;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Damage;
using Robust.Shared.Audio.Systems;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class SignalNpcSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalNpcComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SignalNpcComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, SignalNpcComponent comp, ComponentInit args)
    {
        var sinkPorts = new HashSet<string>();
        var sourcePorts = new HashSet<string>();

        foreach (var response in comp.Responses)
        {
            sinkPorts.Add(response.Port);

            if (!string.IsNullOrWhiteSpace(response.ForwardPort))
                sourcePorts.Add(response.ForwardPort!);
        }

        foreach (var sinkPort in sinkPorts)
        {
            _deviceLink.EnsureSinkPorts(uid, sinkPort);
        }

        foreach (var sourcePort in sourcePorts)
        {
            _deviceLink.EnsureSourcePorts(uid, sourcePort);
        }
    }

    private void OnSignalReceived(EntityUid uid, SignalNpcComponent comp, ref SignalReceivedEvent args)
    {
        foreach (var response in comp.Responses)
        {
            if (response.Port != args.Port)
                continue;

            if (!string.IsNullOrWhiteSpace(response.EmoteId))
                _chat.TryEmoteWithChat(uid, response.EmoteId!, ChatTransmitRange.Normal, ignoreActionBlocker: true, forceEmote: true);

            if (!string.IsNullOrWhiteSpace(response.Message))
                _chat.TrySendInGameICMessage(uid, $">{response.Message}", InGameICChatType.Speak, false, ignoreActionBlocker: true, forced: true);

            if (response.Damage != null)
                _damageable.TryChangeDamage(uid, response.Damage, origin: uid);

            if (response.Sound != null)
                _audio.PlayPvs(response.Sound, uid);

            if (!string.IsNullOrWhiteSpace(response.ForwardPort))
                _deviceLink.InvokePort(uid, response.ForwardPort!, args.Data);
        }
    }
}
