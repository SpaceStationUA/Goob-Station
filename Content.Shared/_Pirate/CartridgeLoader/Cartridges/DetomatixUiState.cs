// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class DetomatixUiState(List<DetomatixTarget> targets, int charges, int maxCharges)
    : BoundUserInterfaceState
{
    public readonly List<DetomatixTarget> Targets = targets;
    public readonly int Charges = charges;
    public readonly int MaxCharges = maxCharges;
}

[Serializable, NetSerializable]
public readonly record struct DetomatixTarget(
    uint Number,
    string Name,
    string? JobTitle,
    string Location,
    bool Armed);
