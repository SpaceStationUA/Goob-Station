// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Speech;

[ByRefEvent]
public sealed class PirateEmoteCooldownAttemptEvent(
    EntityUid source,
    ProtoId<EmotePrototype> emote) : CancellableEntityEventArgs
{
    public EntityUid Source = source;
    public ProtoId<EmotePrototype> Emote = emote;
}

[ByRefEvent]
public sealed class PirateEmoteCooldownCommitEvent(
    EntityUid source,
    ProtoId<EmotePrototype> emote) : EntityEventArgs
{
    public EntityUid Source = source;
    public ProtoId<EmotePrototype> Emote = emote;
}
