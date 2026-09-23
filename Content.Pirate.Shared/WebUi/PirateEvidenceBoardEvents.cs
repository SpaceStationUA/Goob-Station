// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
namespace Content.Pirate.Shared.WebUi;

/// <summary>
///     Client → server: evidence board operation (or "sync") for the
///     station records console. Mutating actions carry a small
///     pipe-separated payload (kept JSON-free by design: engine-side
///     parsing stays hand-rolled, same sandbox rules as the theme/radio
///     bridges).
/// </summary>
[Serializable, NetSerializable]
public sealed class EvidenceBoardRequestEvent : EntityEventArgs
{
    public NetEntity Console;
    public string Action = "";
    public string Data = "";

    /// <summary>"portrait" op: client-rendered PNG of the pinned
    /// character (spawn-time profile snapshot, dummy render).</summary>
    public byte[]? Image;

    /// <summary>pinchar: the (station, id) key of the general record, for
    /// the criminal-record portrait/profile snapshot lookup.</summary>
    public uint RecordKey;
    public NetEntity OriginStation;
}

/// <summary>
///     Server → pinning client: render this profile snapshot into a PNG
///     (ContentSpriteSystem.Export path) and reply via a "portrait"
///     request placing the image on the card.
/// </summary>
[Serializable, NetSerializable]
public sealed class EvidenceBoardPortraitQuestEvent : EntityEventArgs
{
    public NetEntity Console;
    public int CardId;
    public uint RecordKey;

    public Content.Shared.Preferences.HumanoidCharacterProfile? Profile;
    public string JobProto = "";
}

/// <summary>Server → client: full board snapshot JSON for this console.</summary>
[Serializable, NetSerializable]
public sealed class EvidenceBoardStateEvent : EntityEventArgs
{
    public NetEntity Console;
    public string Snapshot = "";
}
