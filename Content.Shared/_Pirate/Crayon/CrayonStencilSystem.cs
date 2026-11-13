// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Pirate.Crayon;

/// <summary>
/// The custom symbol each player draws with: picked in any crayon window, stamped by any crayon,
/// cleared when they pick a stock decal. The server sets it; clients read it.
/// </summary>
public sealed class CrayonStencilSystem : EntitySystem
{
    /// <summary>
    /// The custom symbol the user stamps with any crayon, if they picked one.
    /// </summary>
    public bool TryGetStencil(Entity<CrayonStencilComponent?> user, [NotNullWhen(true)] out CrayonSymbol? symbol)
    {
        symbol = null;
        if (!Resolve(user, ref user.Comp, false))
            return false;

        symbol = user.Comp.Symbol;
        return symbol != null;
    }

    public bool TrySetStencil(EntityUid user, CrayonSymbol symbol)
    {
        if (!symbol.IsValid())
            return false;

        var comp = EnsureComp<CrayonStencilComponent>(user);
        comp.Symbol = symbol;
        Dirty(user, comp);
        return true;
    }

    public void ClearStencil(Entity<CrayonStencilComponent?> user)
    {
        if (!Resolve(user, ref user.Comp, false) || user.Comp.Symbol == null)
            return;

        user.Comp.Symbol = null;
        Dirty(user, user.Comp);
    }
}
