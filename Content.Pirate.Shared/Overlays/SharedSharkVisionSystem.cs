// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Overlays;
using Content.Shared.Actions;

namespace Content.Pirate.Shared.Overlays;

public sealed class SharedSharkVisionSystem : SwitchableOverlaySystem<SharkVisionComponent, ToggleSharkVisionEvent>;

public sealed partial class ToggleSharkVisionEvent : InstantActionEvent;
