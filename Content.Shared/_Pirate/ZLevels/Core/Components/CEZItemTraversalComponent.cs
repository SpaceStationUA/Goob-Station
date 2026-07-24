/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Runtime-only marker for items whose movement can interact with Z-level traversal.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CEZItemTraversalComponent : Component;
