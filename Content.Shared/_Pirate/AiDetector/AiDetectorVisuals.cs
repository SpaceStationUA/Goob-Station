// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.AiDetector;

[Serializable, NetSerializable]
public enum AiDetectorVisuals : byte
{
    Layer,
    Light,
}
