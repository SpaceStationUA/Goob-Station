// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Whitelist;

/// <summary>
/// Marks an AI eye that can be found by an AI detector.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AiDetectableComponent : Component;
