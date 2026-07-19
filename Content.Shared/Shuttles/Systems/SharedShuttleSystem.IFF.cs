// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;

namespace Content.Shared.Shuttles.Systems;

public abstract partial class SharedShuttleSystem
{
    /*
     * Handles the label visibility on radar controls. This can be hiding the label or applying other effects.
     */

    protected virtual void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component) {}

    public Color GetIFFColor(EntityUid gridUid, bool self = false, IFFComponent? component = null)
    {
        if (self)
        {
            return IFFComponent.SelfColor;
        }

        if (!Resolve(gridUid, ref component, false))
        {
            return IFFComponent.IFFColor;
        }

        return component.Color;
    }

    public string? GetIFFLabel(EntityUid gridUid, bool self = false, IFFComponent? component = null)
    {
        // Pirate: multiz - use the lowest linked deck as the display-name source.
        var nameSource = ResolveLinkedGridForLabel(gridUid);
        var entName = TryComp<MetaDataComponent>(nameSource, out var nameMeta) ? nameMeta.EntityName : string.Empty;

        if (self)
        {
            return entName;
        }

        if (Resolve(gridUid, ref component, false) && (component.Flags & (IFFFlags.HideLabel | IFFFlags.Hide)) != 0x0)
        {
            return null;
        }

        return string.IsNullOrEmpty(entName) ? Loc.GetString("shuttle-console-unknown") : entName;
    }

    #region Pirate: multiz
    /// <summary>
    /// Returns the lowest linked deck for shared IFF labels.
    /// </summary>
    private EntityUid ResolveLinkedGridForLabel(EntityUid gridUid)
    {
        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return gridUid;

        var bestUid = gridUid;
        var bestDepth = linked.Depth;
        foreach (var (depth, peer) in linked.PeerGrids)
        {
            if (depth < bestDepth && Exists(peer))
            {
                bestDepth = depth;
                bestUid = peer;
            }
        }
        return bestUid;
    }
    #endregion

    /// <summary>
    /// Sets the color for this grid to appear as on radar.
    /// </summary>
    [PublicAPI]
    public void SetIFFColor(EntityUid gridUid, Color color, IFFComponent? component = null)
    {
        component ??= EnsureComp<IFFComponent>(gridUid);

        if (component.Color.Equals(color))
            return;

        component.Color = color;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }

    [PublicAPI]
    public void AddIFFFlag(EntityUid gridUid, IFFFlags flags, IFFComponent? component = null)
    {
        component ??= EnsureComp<IFFComponent>(gridUid);

        if ((component.Flags & flags) == flags)
            return;

        component.Flags |= flags;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }

    [PublicAPI]
    public void RemoveIFFFlag(EntityUid gridUid, IFFFlags flags, IFFComponent? component = null)
    {
        if (!Resolve(gridUid, ref component, false))
            return;

        if ((component.Flags & flags) == 0x0)
            return;

        component.Flags &= ~flags;
        Dirty(gridUid, component);
        UpdateIFFInterfaces(gridUid, component);
    }
}