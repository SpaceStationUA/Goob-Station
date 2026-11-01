// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared._Pirate.Crayon;
using Content.Shared.Crayon;
using Robust.Client.Player;

namespace Content.Client.Crayon;

public sealed partial class CrayonSystem
{
    /// <summary>
    /// What a held crayon's status says it draws: the player's custom symbol while they have one picked,
    /// since every crayon stamps it then, else the crayon's own decal.
    /// </summary>
    private static string PirateState(Entity<CrayonComponent> crayon)
    {
        // The status control has no system of its own to ask.
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        var stencil = IoCManager.Resolve<IEntityManager>().System<CrayonStencilSystem>();
        return player is { } user && stencil.TryGetStencil(user, out _)
            ? Robust.Shared.Localization.Loc.GetString("pirate-crayon-status-custom")
            : crayon.Comp.SelectedState;
    }
}
