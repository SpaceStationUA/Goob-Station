// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Charges;

/// <summary>
/// Appearance data exposed by limited-charge entities.
/// </summary>
[Serializable, NetSerializable]
public enum LimitedChargesState : byte
{
    HasCharges,
}
