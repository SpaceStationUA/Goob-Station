// SPDX-License-Identifier: AGPL-3.0-or-later

// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

using System.Linq; // Pirate: multiz
using System.Numerics; // Pirate: multiz
using Content.Server.Shuttles.Systems;
using Content.Shared._Mono.FireControl;
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared._Pirate.ZLevels.Core.EntitySystems; // Pirate: multiz
using Content.Shared._Pirate.ZLevels.FireControl; // Pirate: multiz
using Content.Shared.Power;
using Content.Shared.Shuttles.BUIStates; // Pirate: multiz
using Robust.Server.GameObjects;
using Robust.Shared.Map; // Pirate: multiz

namespace Content.Server._Mono.FireControl;

public sealed partial class FireControlSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoleSystem = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!; // Pirate: multiz

    private const int DefaultGunLayerReach = 1; // Pirate: multiz

    private void InitializeConsole()
    {
        SubscribeLocalEvent<FireControlConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FireControlConsoleComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<FireControlConsoleComponent, FireControlConsoleRefreshServerMessage>(OnRefreshServer);
        SubscribeLocalEvent<FireControlConsoleComponent, FireControlConsoleFireMessage>(OnFire);
        SubscribeLocalEvent<FireControlConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<FireControlConsoleComponent, FireControlConsoleSelectLayerMessage>(OnSelectLayer); // Pirate: multiz
    }

    private void OnPowerChanged(EntityUid uid, FireControlConsoleComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegisterConsole(uid, component);
        else
            UnregisterConsole(uid, component);
    }

    private void OnComponentShutdown(EntityUid uid, FireControlConsoleComponent component, ComponentShutdown args)
    {
        UnregisterConsole(uid, component);
    }

    private void OnRefreshServer(EntityUid uid, FireControlConsoleComponent component, FireControlConsoleRefreshServerMessage args)
    {
        if (component.ConnectedServer == null)
        {
            TryRegisterConsole(uid, component);
        }

        if (component.ConnectedServer != null &&
            TryComp<FireControlServerComponent>(component.ConnectedServer, out var server) &&
            server.ConnectedGrid != null)
        {
            RefreshControllables((EntityUid)server.ConnectedGrid);
        }
    }

    private void OnFire(EntityUid uid, FireControlConsoleComponent component, FireControlConsoleFireMessage args)
    {
        if (component.ConnectedServer == null || !TryComp<FireControlServerComponent>(component.ConnectedServer, out var server))
            return;

        // Fire the actual weapons
        FireWeapons((EntityUid)component.ConnectedServer, args.Selected, args.Coordinates, server);

        // Raise an event to track the cursor position even when not firing
        var fireEvent = new FireControlConsoleFireEvent(args.Coordinates, args.Selected);
        RaiseLocalEvent(uid, fireEvent);
    }

    public void OnUIOpened(EntityUid uid, FireControlConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, component);
    }

    #region Pirate: multiz
    private void OnSelectLayer(EntityUid uid, FireControlConsoleComponent component, FireControlConsoleSelectLayerMessage args)
    {
        component.SelectedLayerDepth = args.Depth;
        UpdateUi(uid, component);
    }
    #endregion Pirate: multiz

    private void UnregisterConsole(EntityUid console, FireControlConsoleComponent? component = null)
    {
        if (!Resolve(console, ref component))
            return;

        if (component.ConnectedServer == null || !TryComp<FireControlServerComponent>(component.ConnectedServer, out var server))
            return;

        server.Consoles.Remove(console);
        component.ConnectedServer = null;
        UpdateUi(console, component);
    }
    private bool TryRegisterConsole(EntityUid console, FireControlConsoleComponent? consoleComponent = null)
    {
        if (!Resolve(console, ref consoleComponent))
            return false;

        var gridServer = TryGetGridServer(console);

        if (gridServer.ServerComponent == null)
            return false;

        if (gridServer.ServerComponent.Consoles.Add(console))
        {
            consoleComponent.ConnectedServer = gridServer.ServerUid;
            UpdateUi(console, consoleComponent);
            return true;
        }
        else
        {
            return false;
        }
    }

    private void UpdateUi(EntityUid uid, FireControlConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        #region Pirate: multiz
        // Build selectable z-layers and collect guns from layer servers.
        var consoleGrid = _xform.GetGrid(uid);
        var hostDepth = GetGridDepth(consoleGrid) ?? 0;

        var networkLayers = GetNetworkLayers(consoleGrid);

        var controllables = new List<FireControllableEntry>();
        var gunDepths = new List<int>();

        foreach (var (depth, layer) in networkLayers)
        {
            if (layer.Grid is not { } gridUid)
                continue;
            if (!TryComp<FireControlGridComponent>(gridUid, out var fcGrid) || fcGrid.ControllingServer is not { } serverUid)
                continue;
            if (!TryComp<FireControlServerComponent>(serverUid, out var serverComp))
                continue;

            foreach (var controllable in serverComp.Controlled)
            {
                if (!Exists(controllable))
                    continue;

                // Clamp bad YAML reach so UI and firing agree.
                var reach = Math.Max(0, TryComp<CEZGunLayerReachComponent>(controllable, out var reachComp) ? reachComp.Reach : DefaultGunLayerReach);

                var entry = new FireControllableEntry
                {
                    NetEntity = EntityManager.GetNetEntity(controllable),
                    Coordinates = GetNetCoordinates(Transform(controllable).Coordinates),
                    Name = MetaData(controllable).EntityName,
                    Depth = depth,
                    Reach = reach,
                };
                controllables.Add(entry);
                gunDepths.Add(depth);
            }
        }

        // Available layers are gun depth +/- reach, limited to existing maps.
        var layers = BuildLayerInfos(networkLayers, gunDepths, controllables);
        var currentLayer = ResolveCurrentLayer(component, layers, hostDepth);

        // Point the radar at the selected layer, using a map anchor for empty layers.
        var navState = BuildNavStateForLayer(uid, currentLayer, networkLayers);
        #endregion Pirate: multiz

        var array = controllables.ToArray();

        var state = new FireControlConsoleBoundInterfaceState( // Pirate: multiz
            component.ConnectedServer != null, // Pirate: multiz
            array, // Pirate: multiz
            navState, // Pirate: multiz
            layers, // Pirate: multiz
            currentLayer); // Pirate: multiz
        _ui.SetUiState(uid, FireControlConsoleUiKey.Key, state);
    }

    #region Pirate: multiz
    /// <summary>
    /// Map plus optional shuttle grid for a selectable z-depth.
    /// </summary>
    private readonly record struct ConsoleNetworkLayer(EntityUid Map, EntityUid? Grid);

    /// <summary>
    /// Returns mapped depths in the console z-network, with peer grids when present.
    /// </summary>
    private SortedDictionary<int, ConsoleNetworkLayer> GetNetworkLayers(EntityUid? consoleGrid)
    {
        var result = new SortedDictionary<int, ConsoleNetworkLayer>();
        if (consoleGrid is null)
            return result;

        var hostMapUid = Transform(consoleGrid.Value).MapUid;
        if (hostMapUid is not { } hostMap)
            return result;

        // Empty mapped layers keep Grid=null but remain selectable.
        var hostDepth = 0;
        var peerGridsByDepth = new Dictionary<int, EntityUid>();
        if (TryComp<CEZLinkedGridComponent>(consoleGrid.Value, out var linked))
        {
            hostDepth = linked.Depth;
            foreach (var (peerDepth, peerGrid) in linked.PeerGrids)
                peerGridsByDepth[peerDepth] = peerGrid;
            peerGridsByDepth[linked.Depth] = consoleGrid.Value;
        }

        result[hostDepth] = new ConsoleNetworkLayer(hostMap, consoleGrid.Value);

        // Use map-offset lookup; linked-grid ZNetwork may be unset during UI updates.
        const int probeRange = 16;

        for (var offset = 1; offset <= probeRange; offset++)
        {
            if (!_zLevels.TryMapOffset(hostMap, offset, out var aboveMap))
                break;
            var depth = hostDepth + offset;
            peerGridsByDepth.TryGetValue(depth, out var g);
            result[depth] = new ConsoleNetworkLayer(aboveMap.Value.Owner, g == default ? null : g);
        }

        for (var offset = 1; offset <= probeRange; offset++)
        {
            if (!_zLevels.TryMapOffset(hostMap, -offset, out var belowMap))
                break;
            var depth = hostDepth - offset;
            peerGridsByDepth.TryGetValue(depth, out var g);
            result[depth] = new ConsoleNetworkLayer(belowMap.Value.Owner, g == default ? null : g);
        }

        return result;
    }

    /// <summary>
    /// Builds selector entries from gun reach, limited to mapped network depths.
    /// </summary>
    private FireControlLayerInfo[] BuildLayerInfos(
        SortedDictionary<int, ConsoleNetworkLayer> networkLayers,
        List<int> gunDepths,
        List<FireControllableEntry> controllables)
    {
        if (networkLayers.Count == 0)
            return Array.Empty<FireControlLayerInfo>();

        int minDepth;
        int maxDepth;

        if (gunDepths.Count == 0)
        {
            minDepth = networkLayers.Keys.First();
            maxDepth = networkLayers.Keys.Last();
        }
        else
        {
            var maxReach = controllables.Max(c => c.Reach);
            minDepth = gunDepths.Min() - maxReach;
            maxDepth = gunDepths.Max() + maxReach;
        }

        return networkLayers
            .Where(kvp => kvp.Key >= minDepth && kvp.Key <= maxDepth)
            .Select(kvp =>
            {
                // Use peer grid when present; map anchor for empty layers.
                var anchor = kvp.Value.Grid ?? kvp.Value.Map;
                return new FireControlLayerInfo(kvp.Key, GetNetEntity(anchor));
            })
            .ToArray();
    }

    /// <summary>
    /// Preserves a valid selected depth, falling back to host or first layer.
    /// </summary>
    private int ResolveCurrentLayer(FireControlConsoleComponent component, FireControlLayerInfo[] layers, int hostDepth)
    {
        if (layers.Length == 0)
            return hostDepth;

        if (component.SelectedLayerDepth is { } selected && layers.Any(l => l.Depth == selected))
            return selected;

        if (layers.Any(l => l.Depth == hostDepth))
        {
            component.SelectedLayerDepth = hostDepth;
            return hostDepth;
        }

        component.SelectedLayerDepth = layers[0].Depth;
        return layers[0].Depth;
    }

    /// <summary>
    /// Builds radar state for the selected depth.
    /// </summary>
    private NavInterfaceState BuildNavStateForLayer(
        EntityUid consoleUid,
        int currentLayer,
        SortedDictionary<int, ConsoleNetworkLayer> networkLayers)
    {
        if (!networkLayers.TryGetValue(currentLayer, out var layer))
            return _shuttleConsoleSystem.GetNavState(consoleUid);

        var consoleXform = Transform(consoleUid);

        if (layer.Grid is { } gridUid && consoleXform.GridUid == gridUid)
            return _shuttleConsoleSystem.GetNavState(consoleUid);

        if (layer.Grid is { } peerGrid)
        {
            // Peer decks share local coordinates; this preserves console world rotation.
            var coords = new EntityCoordinates(peerGrid, consoleXform.LocalPosition);
            return _shuttleConsoleSystem.GetNavState(consoleUid, coords, consoleXform.LocalRotation);
        }

        // Empty layers need world rotation because map anchors have zero rotation.
        var consoleWorldPos = _xform.GetWorldPosition(consoleUid);
        var consoleWorldRot = _xform.GetWorldRotation(consoleUid);
        var mapCoords = new EntityCoordinates(layer.Map, consoleWorldPos);
        return _shuttleConsoleSystem.GetNavState(consoleUid, mapCoords, consoleWorldRot);
    }
    #endregion Pirate: multiz
}
