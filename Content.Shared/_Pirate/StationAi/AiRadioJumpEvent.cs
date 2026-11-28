// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationAi;

[Serializable, NetSerializable]
public sealed class AiRadioJumpEvent(NetEntity speaker) : EntityEventArgs
{
    public readonly NetEntity Speaker = speaker;
}
