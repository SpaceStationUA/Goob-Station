// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.NanoChat;

/// <summary>
///     Raised when something that decides NanoChat coverage has changed: a telecommunication server, a
///     transmitter or a Syndicate relay losing or regaining power.
/// </summary>
[ByRefEvent]
public readonly record struct NanoChatNetworkChangedEvent;
