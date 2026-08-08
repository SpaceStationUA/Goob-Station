// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Heretic.Lock;

/// <summary>
/// Marks a status-effect entity which prevents its target from passing access checks.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AccessDeniedStatusEffectComponent : Component;
