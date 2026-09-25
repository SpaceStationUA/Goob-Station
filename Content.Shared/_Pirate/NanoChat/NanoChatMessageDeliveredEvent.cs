// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._DV.CartridgeLoader.Cartridges;

namespace Content.Shared._Pirate.NanoChat;

/// <summary>
///     Raised once per NanoChat message that was actually delivered to at least one card.
/// </summary>
/// <remarks>
///     Deliberately separate from <see cref="NanoChatMessageReceivedEvent" />, which also fires for
///     failed outgoing attempts and once per receiving card. This one fires exactly once per
///     successful send, so listeners never record duplicates when several cards share a number.
/// </remarks>
/// <param name="DeliveryId">
///     Identifies this delivery for as long as the server is up. Several
///     machines record the same send, so this is what lets a federated viewer show it once.
/// </param>
[ByRefEvent]
public readonly record struct NanoChatMessageDeliveredEvent(
    ulong DeliveryId,
    EntityUid? SenderCard,
    EntityUid? SenderDevice,
    uint SenderNumber,
    string? SenderNameOverride,
    uint RecipientNumber,
    IReadOnlyList<EntityUid> RecipientCards,
    NanoChatMessage Message);
