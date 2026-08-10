// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Pirate.SlimeMorph;

/// <summary>
/// Raised on a slimeperson when they use the "Morph" action, opening the self-customization / mimic menu.
/// </summary>
public sealed partial class OpenSlimeMorphUiEvent : InstantActionEvent;

// slime morph immunity trait - start
public sealed class SlimeMorphStudyAttemptEvent(EntityUid user) : CancellableEntityEventArgs
{
    public readonly EntityUid User = user;
}
// slime morph immunity trait - end
