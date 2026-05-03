// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Photo;

[RegisterComponent]
public sealed partial class PhotoCardVisualsComponent : Component
{
}

[Serializable, NetSerializable]
public enum PhotoCardVisuals : byte
{
    PreviewImage
}

public enum PhotoCardVisualLayers : byte
{
    Base,
    Preview
}


