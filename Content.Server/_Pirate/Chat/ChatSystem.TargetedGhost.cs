// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Speech;
using Content.Goobstation.Shared.Emoting;
using Content.Pirate.Shared;
using Content.Pirate.Shared.Components;
using Content.Server.Emoting.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.Muting;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private Filter? GetSpiritAudience(EntityUid source, ChatTransmitRange range)
    {
        if (!TryComp<GhostTargetingComponent>(source, out var spirit) || !spirit.IsGhost)
            return null;

        var audience = Filter.Empty();
        AddListener(source);
        if (spirit.Target != NetEntity.Invalid && TryGetEntity(spirit.Target, out var target) &&
            !HasComp<GhostComponent>(target) && !_mobStateSystem.IsDead(target.Value))
            AddListener(target.Value);

        return audience;

        void AddListener(EntityUid uid)
        {
            if (_playerManager.TryGetSessionByEntity(uid, out var session) &&
                MessageRangeCheck(session, new ICChatRecipientData(0, HasComp<GhostHearingComponent>(uid)), range)
                    != MessageRangeCheckResult.Disallowed)
                audience.AddPlayer(session);
        }
    }

    protected override bool TryHandlePrivateEmote(EntityUid uid, EmotePrototype emote)
    {
        if (GetSpiritAudience(uid, ChatTransmitRange.Normal) is not { } audience)
            return false;

        if (emote.Event != null && HasComp<AnimatedEmotesComponent>(uid))
            RaiseNetworkEvent(new TargetedGhostEmoteMessage(GetNetEntity(uid), emote.ID), audience);

        EmoteSoundsPrototype? sounds = null;
        if (emote.Category.HasFlag(EmoteCategory.Vocal) && !HasComp<MutedComponent>(uid) &&
            TryComp<VocalComponent>(uid, out var vocal))
        {
            var ev = new GetEmoteSoundsEvent();
            RaiseLocalEvent(uid, ref ev);
            var soundId = ev.Handled ? ev.EmoteSoundProtoId : vocal.EmoteSounds?.Id;
            if (soundId != null)
                _prototypeManager.TryIndex<EmoteSoundsPrototype>(soundId, out sounds);
        }
        else if (TryComp<BodyEmotesComponent>(uid, out var body) &&
                 (emote.Category == EmoteCategory.General ||
                  emote.Category.HasFlag(EmoteCategory.Hands) && TryComp<HandsComponent>(uid, out var hands) && hands.Count > 0))
        {
            sounds = body.Sounds;
        }

        TryPlayEmoteSound(uid, sounds, emote.ID, null, audience);
        return true;
    }
}
