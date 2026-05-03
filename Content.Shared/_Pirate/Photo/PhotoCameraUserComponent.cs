// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Photo;

[RegisterComponent]
public sealed partial class PhotoCameraUserComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> AlertPrototype = "PhotoCameraUsed";
}


