// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from CMU.
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Shooting;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Toggle marker for "shoot down through floor opening". Set via the
/// <c>CEToggleShootDownZLevel</c> keybind (Ctrl+Shift+Space). Mutually exclusive with
/// <see cref="CEZLevelViewerComponent.LookUp"/> — the latter routes shots upward instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(CMUZLevelShootingSystem), typeof(CESharedZLevelsSystem))]
public sealed partial class CMUZLevelShooterComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ShootDown;
}
