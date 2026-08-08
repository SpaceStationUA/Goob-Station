// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Billiards;

[Serializable, NetSerializable]
public enum BilliardVisuals : byte
{
    Color,
    Stripe,
}

[Serializable, NetSerializable]
public enum BilliardVisualLayers : byte
{
    Base,
    Stripe,
}

[Serializable, NetSerializable]
public enum BilliardGameType : byte
{
    Pyramid,
    AmericanPool,
}
