/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Runtime-only marker used to route movement events to active Z-physics bodies.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CEZPhysicsActiveComponent : Component;
