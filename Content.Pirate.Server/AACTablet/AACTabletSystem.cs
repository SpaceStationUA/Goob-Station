// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.AACTablet;
using Content.Pirate.Shared.QuickPhrase;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Speech.Components;
using Content.Shared.Abilities.Mime;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.AACTablet;

public sealed class AACTabletSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    private readonly List<string> _localisedPhrases = [];

    public const int MaxPhrases = 10; // no writing novels

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AACTabletComponent, AACTabletSendPhraseMessage>(OnSendPhrase);

        Subs.BuiEvents<AACTabletComponent>(AACTabletKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBoundUIOpened);
        });
    }

    private HashSet<ProtoId<RadioChannelPrototype>> GetAvailableChannels(EntityUid entity)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>();

        // Get all the intrinsic radio channels (IPCs, implants)
        if (TryComp(entity, out ActiveRadioComponent? intrinsicRadio))
            channels.UnionWith(intrinsicRadio.Channels);

        // Get the user's headset channels, if any
        if (TryComp(entity, out WearingHeadsetComponent? headset)
            && TryComp(headset.Headset, out ActiveRadioComponent? headsetRadio))
            channels.UnionWith(headsetRadio.Channels);

        return channels;
    }

    private List<ProtoId<QuickPhrasePrototype>>? GetAvailablePhrases(Entity<AACTabletComponent> ent)
    {
        if (ent.Comp.PhraseGroup is not { } phraseGroup)
            return null;

        return _prototype.Resolve(phraseGroup, out QuickPhraseGroupPrototype? group)
            ? group.Prototypes
            : [];
    }

    private bool IsPhraseAllowed(Entity<AACTabletComponent> ent, ProtoId<QuickPhrasePrototype> phraseId)
    {
        if (ent.Comp.PhraseGroup is { } phraseGroup)
        {
            return _prototype.Resolve(phraseGroup, out QuickPhraseGroupPrototype? group) &&
                   group.Prototypes.Contains(phraseId);
        }

        return _prototype.Resolve(phraseId, out QuickPhrasePrototype? phrase) &&
               !phrase.HiddenFromDefault;
    }

    private void OnBoundUIOpened(Entity<AACTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        var message = new AACTabletUpdateChannelsMessage(
            GetAvailableChannels(args.Actor),
            GetAvailablePhrases(ent));
        _userInterface.ServerSendUiMessage(args.Entity, AACTabletKey.Key, message, args.Actor);
    }

    private void OnSendPhrase(Entity<AACTabletComponent> ent, ref AACTabletSendPhraseMessage message)
    {
        if (ent.Comp.NextPhrase > _timing.CurTime || message.PhraseIds.Count > MaxPhrases)
            return;

        if (message.PhraseIds.Any(phrase => !IsPhraseAllowed(ent, phrase)))
            return;

        if (TryComp<MimePowersComponent>(message.Actor, out var mimePowers) && mimePowers.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("mime-cant-use-AAC-tablet"), message.Actor, message.Actor);
            return;
        }

        var availableChannels = GetAvailableChannels(message.Actor);
        var prefix = SharedChatSystem.LocalPrefix.ToString();

        if (message.Channel is { } channel)
        {
            if (!availableChannels.Contains(channel)
                || !_prototype.Resolve(channel, out RadioChannelPrototype? channelPrototype))
            {
                return;
            }

            prefix = string.Concat(SharedChatSystem.RadioChannelPrefix, channelPrototype.KeyCode);
        }

        var senderName = Identity.Entity(message.Actor, EntityManager);
        var speakerName = Loc.GetString("speech-name-relay",
            ("speaker", Name(ent)),
            ("originalName", senderName));

        _localisedPhrases.Clear();
        foreach (var phraseProto in message.PhraseIds)
        {
            if (_prototype.Resolve(phraseProto, out var phrase))
            {
                // Ensures each phrase is capitalised to maintain common AAC styling
                _localisedPhrases.Add(_chat.SanitizeMessageCapital(Loc.GetString(phrase.Text)));
            }
        }

        if (_localisedPhrases.Count <= 0)
            return;

        EnsureComp<VoiceOverrideComponent>(ent).NameOverride = speakerName;

        // Set the player's currently available channels before sending the message
        EnsureComp(ent, out IntrinsicRadioTransmitterComponent transmitter);
        transmitter.Channels = availableChannels;

        // Pirate: save the message for logging.
        var messageToSend = string.Join(" ", _localisedPhrases);

        _chat.TrySendInGameICMessage(ent,
            prefix + messageToSend,
            InGameICChatType.Speak,
            hideChat: false,
            nameOverride: speakerName);

        // Pirate: log AAC chat message.
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"AAC tablet message from {ToPrettyString(message.Actor):user}: {messageToSend}");

        var curTime = _timing.CurTime;
        ent.Comp.NextPhrase = curTime + ent.Comp.Cooldown;
    }
}
