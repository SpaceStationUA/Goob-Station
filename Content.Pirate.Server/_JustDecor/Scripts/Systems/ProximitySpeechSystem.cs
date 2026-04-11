using System;
using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.ProximityDetection;
using Robust.Shared.Timing;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class ProximitySpeechSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProximitySpeechComponent, NewProximityTargetEvent>(OnNewTarget);
    }

    private void OnNewTarget(EntityUid uid, ProximitySpeechComponent comp, ref NewProximityTargetEvent args)
    {
        if (comp.Once && comp.HasTriggered)
            return;

        if (_timing.CurTime < comp.NextAllowedSpeak)
            return;

        if (!string.IsNullOrWhiteSpace(comp.EmoteId))
            _chat.TryEmoteWithChat(uid, comp.EmoteId!, ChatTransmitRange.Normal, ignoreActionBlocker: true, forceEmote: true);

        if (!string.IsNullOrWhiteSpace(comp.Message))
            _chat.TrySendInGameICMessage(uid, $">{comp.Message}", InGameICChatType.Speak, false, ignoreActionBlocker: true, forced: true);

        comp.HasTriggered = true;
        comp.NextAllowedSpeak = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
    }
}
