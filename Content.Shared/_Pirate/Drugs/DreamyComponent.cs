// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Drugs;

/// <summary>
///     Adds a shader to the client that scales with the effect duration.
///     Use only in conjunction with <see cref="StatusEffectComponent"/>, on the status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DreamyStatusEffectComponent : Component;
