// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Pirate.Server.WebUi;

/// <summary>
///     Shared ownership checks for the device WebUI apps (radio, theme picker).
///
///     These apps live inside a cartridge whose parent is the player's PDA, so
///     a client message only carries a NetEntity -- a small integer the client
///     picks, not one the server chose. Nothing about it is trustworthy: the
///     value could name any entity on the server, including another player's
///     PDA or a crate in a sealed vault. Every handler that acts on a
///     client-supplied marker must therefore prove the sender's own player
///     entity is an ancestor of that marker before doing anything with it.
/// </summary>
public static class PirateWebUiOwnership
{
    /// <summary>Depth bound for the parent walk. A PDA on a player looks like
    /// app -> pda -> [hand | slot] -> player (3); a bagged PDA adds a couple.
    /// 12 is generous and still cannot be walked off into a cycle.</summary>
    private const int MaxDepth = 12;

    /// <summary>
    ///     True when <paramref name="entity"/> is the sender's own held/worn
    ///     gear, or anything contained within it. Works for the cartridge
    ///     itself and for the PDA it sits in.
    /// </summary>
    public static bool SenderOwns(IEntityManager entMan, EntitySessionEventArgs args, EntityUid entity)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null || !entMan.EntityExists(player.Value))
            return false;

        EntityUid cursor = entity;
        for (var depth = 0; depth <= MaxDepth; depth++)
        {
            if (cursor == player.Value)
                return true;

            if (!entMan.TryGetComponent<TransformComponent>(cursor, out var xform) ||
                !xform.ParentUid.IsValid())
            {
                return false;
            }

            cursor = xform.ParentUid;
        }

        return false;
    }

    /// <summary>
    ///     Resolve a client-supplied marker to the device (PDA) it belongs to.
    ///     The marker is normally the cartridge app; when it is the device
    ///     itself, <paramref name="device"/> comes back as the marker.
    /// </summary>
    public static bool TryGetDevice(IEntityManager entMan, EntityUid marker, out EntityUid device)
    {
        if (entMan.TryGetComponent<TransformComponent>(marker, out var xform) && xform.ParentUid.IsValid())
        {
            device = xform.ParentUid;
            return true;
        }

        device = marker;
        return true;
    }
}
