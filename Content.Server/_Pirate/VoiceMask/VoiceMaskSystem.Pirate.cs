// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.VoiceMask;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.VoiceMask;

public sealed partial class VoiceMaskSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private void InitializePirate()
    {
        SubscribeLocalEvent<VoiceMaskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskSetModeMessage>(OnSetMode);
    }

    private void OnMapInit(Entity<VoiceMaskComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.AltToggle)
            return;

        var toggle = EnsureComp<VoiceMaskToggleComponent>(ent);
        toggle.Active = ent.Comp.Active;
        Dirty(ent, toggle);
    }

    public void SetActive(Entity<VoiceMaskComponent> ent, bool active, EntityUid user)
    {
        ent.Comp.Active = active;
        if (TryComp<VoiceMaskToggleComponent>(ent, out var toggle))
        {
            toggle.Active = active;
            Dirty(ent, toggle);
        }

        _popupSystem.PopupEntity(Loc.GetString(active ? "voice-mask-popup-enabled" : "voice-mask-popup-disabled"), user, user);
        _audio.PlayEntity(ent.Comp.ToggleSound, user, user);

        _identity.QueueIdentityUpdate(GetWearer(ent) ?? user);
        UpdateUI(ent);
    }

    private void OnSetMode(Entity<VoiceMaskComponent> ent, ref VoiceMaskSetModeMessage msg)
    {
        switch (msg.Mode)
        {
            case SpeakerNameContext.Whisper:
                ent.Comp.TransformWhisper = msg.Enabled;
                break;
            case SpeakerNameContext.Radio:
                if (msg.Channel is not { } channel || !_proto.HasIndex(channel))
                    return;

                if (msg.Enabled)
                    ent.Comp.ExcludedRadioChannels.Remove(channel);
                else
                    ent.Comp.ExcludedRadioChannels.Add(channel);
                break;
            default:
                ent.Comp.TransformSay = msg.Enabled;
                break;
        }

        UpdateUI(ent);
    }

    private static bool ShouldTransformMode(VoiceMaskComponent comp, TransformSpeakerNameEvent args)
    {
        return args.Context switch
        {
            SpeakerNameContext.Whisper => comp.TransformWhisper,
            SpeakerNameContext.Radio => args.RadioChannel is not { } channel || !comp.ExcludedRadioChannels.Contains(channel),
            _ => comp.TransformSay,
        };
    }

    private VoiceMaskModesState GetModesState(Entity<VoiceMaskComponent> ent)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>> { SharedChatSystem.CommonChannel };
        channels.UnionWith(ent.Comp.ExcludedRadioChannels);

        if (GetWearer(ent) is { } wearer)
        {
            if (TryComp<IntrinsicRadioTransmitterComponent>(wearer, out var intrinsic))
                channels.UnionWith(intrinsic.Channels);

            var slots = _inventory.GetSlotEnumerator(wearer);
            while (slots.NextItem(out var item))
            {
                if (TryComp<EncryptionKeyHolderComponent>(item, out var keys))
                    channels.UnionWith(keys.Channels);
            }
        }

        var list = channels
            .Where(c => _proto.HasIndex(c))
            .OrderBy(c => _proto.Index(c).LocalizedName)
            .Select(c => new VoiceMaskChannelMode(c, !ent.Comp.ExcludedRadioChannels.Contains(c)))
            .ToList();

        return new VoiceMaskModesState(ent.Comp.TransformSay, ent.Comp.TransformWhisper, list);
    }

    private EntityUid? GetWearer(EntityUid mask)
    {
        return _container.TryGetContainingContainer(mask, out var container) ? container.Owner : null;
    }
}
