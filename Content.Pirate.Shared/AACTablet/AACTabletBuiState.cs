// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.AACTablet;

[Serializable, NetSerializable]
public sealed class AACTabletBuiState(HashSet<ProtoId<RadioChannelPrototype>> radioChannels) : BoundUserInterfaceState
{
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = radioChannels;
}
