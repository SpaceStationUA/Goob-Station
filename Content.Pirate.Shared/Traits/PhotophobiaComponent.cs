// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Traits;

/// <summary>
/// Makes bright areas harder to see and flashlight beams capable of flashing the entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PhotophobiaComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FlashDuration = 2f;

    [DataField, AutoNetworkedField]
    public float FlashSlowdown = 1f;

    [DataField, AutoNetworkedField]
    public float ShaderStrengthMultiplier = 1f;
}
