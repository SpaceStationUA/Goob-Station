// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.QuickPhrase;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.AACTablet;

[Serializable, NetSerializable]
public enum AACTabletKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AACTabletUpdateChannelsMessage(
    HashSet<ProtoId<RadioChannelPrototype>> radioChannels,
    List<ProtoId<QuickPhrasePrototype>>? phraseIds) : BoundUserInterfaceMessage
{
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = radioChannels;
    public List<ProtoId<QuickPhrasePrototype>>? PhraseIds = phraseIds;
}

[Serializable, NetSerializable]
public sealed class AACTabletSendPhraseMessage(
    List<ProtoId<QuickPhrasePrototype>> phraseIds,
    ProtoId<RadioChannelPrototype>? channel) : BoundUserInterfaceMessage
{
    public List<ProtoId<QuickPhrasePrototype>> PhraseIds = phraseIds;
    public ProtoId<RadioChannelPrototype>? Channel = channel;
}
