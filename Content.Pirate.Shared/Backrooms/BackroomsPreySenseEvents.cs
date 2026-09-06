// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Backrooms;

public sealed partial class BackroomsPreySenseActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum BackroomsPreySenseUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public readonly record struct BackroomsPreySenseTarget(NetEntity Target, string DisplayName);

[Serializable, NetSerializable]
public sealed partial class BackroomsPreySenseBuiState : BoundUserInterfaceState
{
    public List<BackroomsPreySenseTarget> Targets { get; init; } = new();
}

[Serializable, NetSerializable]
public sealed partial class BackroomsPreySenseSelectedBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity Target { get; init; }
}
