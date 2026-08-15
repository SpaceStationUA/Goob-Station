// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class DetomatixUiMessageEvent(uint targetNumber) : CartridgeMessageEvent
{
    public readonly uint TargetNumber = targetNumber;
}
