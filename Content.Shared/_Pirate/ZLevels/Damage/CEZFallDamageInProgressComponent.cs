/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Damage;

/// <summary>
/// Transient marker added to an entity only for the duration of the synchronous
/// <c>TryChangeDamage</c> call that applies z-level fall damage to it (see
/// <see cref="CEZLevelDamageSystem"/>). It lets other systems tell z-level fall damage apart from
/// ordinary damage while reacting to <c>DamageChangedEvent</c> — for example a mech uses it to take
/// the fall damage itself without forwarding a share of it to the pilot inside.
/// Never persisted or networked; it is added and removed within the same tick.
/// </summary>
[RegisterComponent]
public sealed partial class CEZFallDamageInProgressComponent : Component
{
}
