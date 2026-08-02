// SPDX-FileCopyrightText: 2025 Impstation contributors

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.QuickPhrase;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.AACTablet;

[Serializable, NetSerializable]
public enum AACTabletKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AACTabletSendPhraseMessage(List<ProtoId<QuickPhrasePrototype>> phraseIds) : BoundUserInterfaceMessage
{
    public List<ProtoId<QuickPhrasePrototype>> PhraseIds = phraseIds;
}
