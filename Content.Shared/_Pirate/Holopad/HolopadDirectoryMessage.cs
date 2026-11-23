// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Holopad;

[Serializable, NetSerializable]
public sealed class HolopadDirectoryRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class HolopadDirectoryMessage(Dictionary<NetEntity, string> holopads) : BoundUserInterfaceMessage
{
    public readonly Dictionary<NetEntity, string> Holopads = holopads;
}
