// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Nuclear.Centrifuge;

[Serializable, NetSerializable]
public enum NuclearCentrifugeVisuals : byte
{
    Processing,
    Layer
}
