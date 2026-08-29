// SPDX-License-Identifier: MIT

using Content.Shared.Actions;

namespace Content.Shared._Pirate.Clothing.EngineeringGoggles;

/// <summary>
/// Pirate: engineering goggles - action event that cycles through Off -> XRay -> Tray -> Off.
/// </summary>
public sealed partial class ToggleEngineeringGogglesEvent : InstantActionEvent;
