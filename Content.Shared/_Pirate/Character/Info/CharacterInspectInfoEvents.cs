// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Character.Info;

// Mind data stays on its owner's client; inspection requests retrieve it from the server.
[Serializable, NetSerializable]
public sealed class RequestCharacterInspectInfoEvent(NetEntity target) : EntityEventArgs
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class CharacterInspectInfoEvent(NetEntity target, string? job, string? personality, string? oocNotes)
    : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly string? Job = job;
    public readonly string? Personality = personality;
    public readonly string? OOCNotes = oocNotes;
}
