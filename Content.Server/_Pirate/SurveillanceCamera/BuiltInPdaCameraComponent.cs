// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.SurveillanceCamera;

/// <summary>Installs a sealed camera inside a PDA without replacing its PDA network connection.</summary>
[RegisterComponent]
public sealed partial class BuiltInPdaCameraComponent : Component
{
    [DataField(required: true)]
    public EntProtoId CameraPrototype;
}
