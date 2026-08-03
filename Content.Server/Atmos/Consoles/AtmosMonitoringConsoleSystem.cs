// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Server.DeviceNetwork.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Consoles;
using Content.Shared.Labels.Components;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared._Pirate.ZLevels.Monitoring; // Pirate: multiz
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.NodeContainer;

namespace Content.Server.Atmos.Consoles;

public sealed class AtmosMonitoringConsoleSystem : SharedAtmosMonitoringConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedMapSystem _sharedMapSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    // Private variables
    // Note: this data does not need to be saved
    private Dictionary<EntityUid, Dictionary<Vector2i, AtmosPipeChunk>> _gridAtmosPipeChunks = new();
    private readonly Dictionary<EntityUid, EntityUid> _selectedMonitorGrids = new(); // Pirate: multiz
    private readonly Dictionary<EntityUid, EntityUid> _appliedMonitorGrids = new(); // Pirate: multiz
    private float _updateTimer = 1.0f;

    // Constants
    private const float UpdateTime = 1.0f;
    private const int ChunkSize = 4;

    public override void Initialize()
    {
        base.Initialize();

        // Console events
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChanged);
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, EntParentChangedMessage>(OnConsoleParentChanged);
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, ComponentShutdown>(OnConsoleShutdown); // Pirate: multiz
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, CEZMonitoringConsoleLevelSelectedMessage>(OnZLevelSelected); // Pirate: multiz

        // Tracked device events
        SubscribeLocalEvent<AtmosMonitoringConsoleDeviceComponent, NodeGroupsRebuilt>(OnEntityNodeGroupsRebuilt);
        SubscribeLocalEvent<AtmosMonitoringConsoleDeviceComponent, AtmosPipeColorChangedEvent>(OnEntityPipeColorChanged);
        SubscribeLocalEvent<AtmosMonitoringConsoleDeviceComponent, EntityTerminatingEvent>(OnEntityShutdown);

        // Grid events
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<PipeNodeGroupRemovedEvent>(OnPipeNodeGroupRemoved);
    }

    #region Event handling

    private void OnConsoleInit(EntityUid uid, AtmosMonitoringConsoleComponent component, ComponentInit args)
    {
        InitializeAtmosMonitoringConsole(uid, component);
    }

    private void OnConsoleAnchorChanged(EntityUid uid, AtmosMonitoringConsoleComponent component, AnchorStateChangedEvent args)
    {
        InitializeAtmosMonitoringConsole(uid, component);
    }

    private void OnConsoleParentChanged(EntityUid uid, AtmosMonitoringConsoleComponent component, EntParentChangedMessage args)
    {
        component.ForceFullUpdate = true;
        InitializeAtmosMonitoringConsole(uid, component);
    }

    #region Pirate: multiz
    private void OnConsoleShutdown(EntityUid uid, AtmosMonitoringConsoleComponent component, ComponentShutdown args)
    {
        _selectedMonitorGrids.Remove(uid);
        _appliedMonitorGrids.Remove(uid);
    }

    private void OnZLevelSelected(EntityUid uid, AtmosMonitoringConsoleComponent component, CEZMonitoringConsoleLevelSelectedMessage args)
    {
        var targetGrid = GetEntity(args.Grid);
        if (targetGrid == null)
            return;

        var xform = Transform(uid);
        if (xform.GridUid == null || !IsValidZMonitoringGrid(xform.GridUid.Value, targetGrid.Value))
            return;

        _selectedMonitorGrids[uid] = targetGrid.Value;
        EnsureComp<NavMapComponent>(targetGrid.Value);
        UpdateAtmosMonitoringConsoleGridData(uid, component, targetGrid.Value);
        _appliedMonitorGrids[uid] = targetGrid.Value;
        UpdateUIState(uid, component, xform);
    }
    private EntityUid GetSelectedMonitoringGrid(EntityUid consoleUid, TransformComponent xform)
    {
        if (xform.GridUid == null)
            return EntityUid.Invalid;

        if (_selectedMonitorGrids.TryGetValue(consoleUid, out var selectedGrid) &&
            IsValidZMonitoringGrid(xform.GridUid.Value, selectedGrid))
        {
            return selectedGrid;
        }

        _selectedMonitorGrids.Remove(consoleUid);
        return xform.GridUid.Value;
    }

    private bool IsValidZMonitoringGrid(EntityUid sourceGrid, EntityUid targetGrid)
    {
        if (sourceGrid == targetGrid)
            return true;

        return TryComp<CEZLinkedGridComponent>(sourceGrid, out var sourceLinked) &&
               TryComp<CEZLinkedGridComponent>(targetGrid, out var targetLinked) &&
               sourceLinked.ZNetwork.IsValid() &&
               sourceLinked.ZNetwork == targetLinked.ZNetwork;
    }

    private void UpdateAtmosMonitoringConsoleGridData(EntityUid uid, AtmosMonitoringConsoleComponent component, EntityUid gridUid)
    {
        component.AtmosDevices = GetAllAtmosDeviceNavMapData(gridUid);

        if (!_gridAtmosPipeChunks.TryGetValue(gridUid, out var chunks))
        {
            if (TryComp<MapGridComponent>(gridUid, out var map))
                RebuildAtmosPipeGrid(gridUid, map);

            _gridAtmosPipeChunks.TryGetValue(gridUid, out chunks);
        }

        component.AtmosPipeChunks = chunks ?? new Dictionary<Vector2i, AtmosPipeChunk>();
        Dirty(uid, component);
    }
    #endregion

    private void OnEntityNodeGroupsRebuilt(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component, NodeGroupsRebuilt args)
    {
        InitializeAtmosMonitoringDevice(uid, component);
    }

    private void OnEntityPipeColorChanged(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component, AtmosPipeColorChangedEvent args)
    {
        InitializeAtmosMonitoringDevice(uid, component);
    }

    private void OnEntityShutdown(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component, EntityTerminatingEvent args)
    {
        ShutDownAtmosMonitoringEntity(uid, component);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        // Collect grids
        var allGrids = args.NewGrids.ToList();

        if (!allGrids.Contains(args.Grid))
            allGrids.Add(args.Grid);

        // Rebuild the pipe networks on the affected grids
        foreach (var ent in allGrids)
        {
            if (!TryComp<MapGridComponent>(ent, out var grid))
                continue;

            RebuildAtmosPipeGrid(ent, grid);
        }

        // Update atmos monitoring consoles that stand upon an updated grid
        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (entXform.GridUid == null)
                continue;

            if (!allGrids.Contains(entXform.GridUid.Value))
                continue;

            InitializeAtmosMonitoringConsole(ent, entConsole);
        }
    }

    #endregion

    #region UI updates

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;

        if (_updateTimer >= UpdateTime)
        {
            _updateTimer -= UpdateTime;

            var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
            while (query.MoveNext(out var ent, out var entConsole, out var entXform))
            {
                if (entXform?.GridUid == null)
                    continue;

                UpdateUIState(ent, entConsole, entXform);
            }
        }
    }

    public void UpdateUIState
        (EntityUid uid,
        AtmosMonitoringConsoleComponent component,
        TransformComponent xform)
    {
        if (!_userInterfaceSystem.IsUiOpen(uid, AtmosMonitoringConsoleUiKey.Key))
            return;

        var gridUid = GetSelectedMonitoringGrid(uid, xform); // Pirate: multiz

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmosphere))
            return;

        #region Pirate: multiz
        // If the resolved grid differs from what we last populated for (e.g. selection was invalidated and
        // GetSelectedMonitoringGrid fell back to the console's grid), repopulate cached chunks/devices.
        if (!_appliedMonitorGrids.TryGetValue(uid, out var appliedGrid) || appliedGrid != gridUid)
        {
            UpdateAtmosMonitoringConsoleGridData(uid, component, gridUid);
            _appliedMonitorGrids[uid] = gridUid;
        }
        #endregion

        // Console init, selection changes, and pipe/device rebuild paths already populate AtmosDevices/AtmosPipeChunks
        // and dirty the component; redoing it every 1-second UI refresh turned an idle console into a constant resender.

        // Gathering data to be send to the client
        var atmosNetworks = new List<AtmosMonitoringConsoleEntry>();
        var query = AllEntityQuery<GasPipeSensorComponent, TransformComponent>();

        while (query.MoveNext(out var ent, out var entSensor, out var entXform))
        {
            if (entXform?.GridUid != gridUid) // Pirate: multiz
                continue;

            if (!entXform.Anchored)
                continue;

            var entry = CreateAtmosMonitoringConsoleEntry(ent, entXform);

            if (entry != null)
                atmosNetworks.Add(entry.Value);
        }

        // Set the UI state
        _userInterfaceSystem.SetUiState(uid, AtmosMonitoringConsoleUiKey.Key,
            new AtmosMonitoringConsoleBoundInterfaceState(atmosNetworks.ToArray()));
    }

    private AtmosMonitoringConsoleEntry? CreateAtmosMonitoringConsoleEntry(EntityUid uid, TransformComponent xform)
    {
        AtmosMonitoringConsoleEntry? entry = null;

        var netEnt = GetNetEntity(uid);
        var name = MetaData(uid).EntityName;
        var address = string.Empty;

        if (xform.GridUid == null)
            return null;

        if (!TryGettingFirstPipeNode(uid, out var pipeNode, out var netId) ||
            pipeNode == null ||
            netId == null)
            return null;

        var pipeColor = TryComp<AtmosPipeColorComponent>(uid, out var colorComponent) ? colorComponent.Color : Color.White;

        // Name the entity based on its label, if available
        if (TryComp<LabelComponent>(uid, out var label) && label.CurrentLabel != null)
            name = label.CurrentLabel;

        // Otherwise use its base name and network address
        else if (TryComp<DeviceNetworkComponent>(uid, out var deviceNet))
            address = deviceNet.Address;

        // Entry for unpowered devices
        if (TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver) && !apcPowerReceiver.Powered)
        {
            entry = new AtmosMonitoringConsoleEntry(netEnt, GetNetCoordinates(xform.Coordinates), netId.Value, name, address)
            {
                IsPowered = false,
                Color = pipeColor
            };

            return entry;
        }

        // Entry for powered devices
        var gasData = new Dictionary<Gas, float>();
        var isAirPresent = pipeNode.Air.TotalMoles > 0;

        if (isAirPresent)
        {
            foreach (var gas in Enum.GetValues<Gas>())
            {
                if (pipeNode.Air[(int)gas] > 0)
                    gasData.Add(gas, pipeNode.Air[(int)gas] / pipeNode.Air.TotalMoles);
            }
        }

        entry = new AtmosMonitoringConsoleEntry(netEnt, GetNetCoordinates(xform.Coordinates), netId.Value, name, address)
        {
            TemperatureData = isAirPresent ? pipeNode.Air.Temperature : 0f,
            PressureData = pipeNode.Air.Pressure,
            TotalMolData = pipeNode.Air.TotalMoles,
            GasData = gasData,
            Color = pipeColor
        };

        return entry;
    }

    private Dictionary<NetEntity, AtmosDeviceNavMapData> GetAllAtmosDeviceNavMapData(EntityUid gridUid)
    {
        var atmosDeviceNavMapData = new Dictionary<NetEntity, AtmosDeviceNavMapData>();

        var query = AllEntityQuery<AtmosMonitoringConsoleDeviceComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entComponent, out var entXform))
        {
            if (TryGetAtmosDeviceNavMapData(ent, entComponent, entXform, gridUid, out var data))
                atmosDeviceNavMapData.Add(data.Value.NetEntity, data.Value);
        }

        return atmosDeviceNavMapData;
    }

    private bool TryGetAtmosDeviceNavMapData
        (EntityUid uid,
        AtmosMonitoringConsoleDeviceComponent component,
        TransformComponent xform,
        EntityUid gridUid,
        [NotNullWhen(true)] out AtmosDeviceNavMapData? device)
    {
        device = null;

        if (component.NavMapBlip == null)
            return false;

        if (xform.GridUid != gridUid)
            return false;

        if (!xform.Anchored)
            return false;

        var direction = xform.LocalRotation.GetCardinalDir();
        var netId = TryGettingFirstPipeNode(uid, out var _, out var firstNetId) ? firstNetId : -1;
        var color = TryComp<AtmosPipeColorComponent>(uid, out var atmosPipeColor) ? atmosPipeColor.Color : Color.White;
        var layer = TryComp<AtmosPipeLayersComponent>(uid, out var atmosPipeLayers) ? atmosPipeLayers.CurrentPipeLayer : AtmosPipeLayer.Primary;

        device = new AtmosDeviceNavMapData(GetNetEntity(uid), GetNetCoordinates(xform.Coordinates), netId.Value, component.NavMapBlip.Value, direction, color, layer);

        return true;
    }

    #endregion

    #region Pipe net functions

    private void OnPipeNodeGroupRemoved(ref PipeNodeGroupRemovedEvent args)
    {
        // When a pipe node group is removed, we need to iterate over all of
        // our pipe chunks and remove any entries with a matching net id.
        // (We only need to check the chunks for the affected grid, though.)

        if (!_gridAtmosPipeChunks.TryGetValue(args.Grid, out var chunkData))
            return;

        foreach (var chunk in chunkData.Values)
        {
            foreach (var key in chunk.AtmosPipeData.Keys)
            {
                if (key.NetId == args.NetId)
                    chunk.AtmosPipeData.Remove(key);
            }
        }
    }

    private void RebuildAtmosPipeGrid(EntityUid gridUid, MapGridComponent grid)
    {
        var allChunks = new Dictionary<Vector2i, AtmosPipeChunk>();

        // Adds all atmos pipes to the nav map via bit mask chunks
        var queryPipes = AllEntityQuery<AtmosPipeColorComponent, NodeContainerComponent, TransformComponent>();
        while (queryPipes.MoveNext(out var ent, out var entAtmosPipeColor, out var entNodeContainer, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            var tile = _sharedMapSystem.GetTileRef(gridUid, grid, entXform.Coordinates);
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile.GridIndices, ChunkSize);
            var relative = SharedMapSystem.GetChunkRelative(tile.GridIndices, ChunkSize);

            if (!allChunks.TryGetValue(chunkOrigin, out var chunk))
            {
                chunk = new AtmosPipeChunk(chunkOrigin);
                allChunks[chunkOrigin] = chunk;
            }

            UpdateAtmosPipeChunk(ent, entNodeContainer, entAtmosPipeColor, GetTileIndex(relative), ref chunk);
        }

        // Add or update the chunks on the associated grid
        _gridAtmosPipeChunks[gridUid] = allChunks;

        // Update the consoles that are on the same grid
        var queryConsoles = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
        while (queryConsoles.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (gridUid != GetSelectedMonitoringGrid(ent, entXform)) // Pirate: multiz
                continue;

            entConsole.AtmosPipeChunks = allChunks;
            entConsole.AtmosDevices = GetAllAtmosDeviceNavMapData(gridUid); // Pirate: multiz
            Dirty(ent, entConsole);
        }
    }

    private void RebuildSingleTileOfPipeNetwork(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords)
    {
        if (!_gridAtmosPipeChunks.TryGetValue(gridUid, out var allChunks))
            allChunks = new Dictionary<Vector2i, AtmosPipeChunk>();

        var tile = _sharedMapSystem.GetTileRef(gridUid, grid, coords);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile.GridIndices, ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile.GridIndices, ChunkSize);
        var tileIdx = GetTileIndex(relative);

        if (!allChunks.TryGetValue(chunkOrigin, out var chunk))
            chunk = new AtmosPipeChunk(chunkOrigin);

        // Remove all stale values for the tile
        foreach (var (index, atmosPipeData) in chunk.AtmosPipeData)
        {
            var mask = (ulong)SharedNavMapSystem.AllDirMask << tileIdx * SharedNavMapSystem.Directions;
            chunk.AtmosPipeData[index] = atmosPipeData & ~mask;
        }

        // Rebuild the tile's pipe data
        foreach (var ent in _sharedMapSystem.GetAnchoredEntities(gridUid, grid, coords))
        {
            if (!TryComp<AtmosPipeColorComponent>(ent, out var entAtmosPipeColor))
                continue;

            if (!TryComp<NodeContainerComponent>(ent, out var entNodeContainer))
                continue;

            var showAbsentConnections = TryComp<AtmosMonitoringConsoleDeviceComponent>(ent, out var device) ? device.ShowAbsentConnections : true;

            UpdateAtmosPipeChunk(ent, entNodeContainer, entAtmosPipeColor, tileIdx, ref chunk, showAbsentConnections);
        }

        // Add or update the chunk on the associated grid
        // Only the modified chunk will be sent to the client
        chunk.LastUpdate = _gameTiming.CurTick;
        allChunks[chunkOrigin] = chunk;
        _gridAtmosPipeChunks[gridUid] = allChunks;

        // Update the components of the monitoring consoles that are attached to the same grid
        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (gridUid != GetSelectedMonitoringGrid(ent, entXform)) // Pirate: multiz
                continue;

            entConsole.AtmosPipeChunks = allChunks;
            // Pirate: multiz - deliberately NOT refreshing AtmosDevices here. This runs once per
            // rebuilt tile, and NodeGroupsRebuilt fires for every device in a rebuilt pipe net, so
            // a full device rescan here is O(devices^2 * consoles) - that is what froze the server
            // when pipes were built, unanchored or blown up on a large station. The device list is
            // already maintained incrementally by InitializeAtmosMonitoringDevice, and a z-level
            // selection change repopulates it through UpdateAtmosMonitoringConsoleGridData.
            Dirty(ent, entConsole);
        }
    }

    private void UpdateAtmosPipeChunk
        (EntityUid uid,
        NodeContainerComponent nodeContainer,
        AtmosPipeColorComponent pipeColor,
        int tileIdx,
        ref AtmosPipeChunk chunk,
        bool showAbsentConnections = true)
    {
        // Entities that are actively being deleted are not to be drawn
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        foreach ((var id, var node) in nodeContainer.Nodes)
        {
            if (node is not PipeNode { } pipeNode)
                continue;

            if (!showAbsentConnections && !pipeNode.ReachableNodes.Any(x => x.Owner != uid))
                continue;

            var netId = GetPipeNodeNetId(pipeNode);
            var subnet = new AtmosMonitoringConsoleSubnet(netId, pipeNode.CurrentPipeLayer, pipeColor.Color);
            var pipeDirection = pipeNode.CurrentPipeDirection;

            chunk.AtmosPipeData.TryGetValue(subnet, out var atmosPipeData);
            atmosPipeData |= (ulong)pipeDirection << tileIdx * SharedNavMapSystem.Directions;
            chunk.AtmosPipeData[subnet] = atmosPipeData;
        }
    }

    private bool TryGettingFirstPipeNode(EntityUid uid, [NotNullWhen(true)] out PipeNode? pipeNode, [NotNullWhen(true)] out int? netId)
    {
        pipeNode = null;
        netId = null;

        if (!TryComp<NodeContainerComponent>(uid, out var nodeContainer))
            return false;

        foreach (var node in nodeContainer.Nodes.Values)
        {
            if (node is PipeNode)
            {
                pipeNode = (PipeNode)node;
                netId = GetPipeNodeNetId(pipeNode);

                return true;
            }
        }

        return false;
    }

    private int GetPipeNodeNetId(PipeNode pipeNode)
    {
        if (pipeNode.NodeGroup is BaseNodeGroup)
        {
            var nodeGroup = (BaseNodeGroup)pipeNode.NodeGroup;

            return nodeGroup.NetId;
        }

        return -1;
    }

    #endregion

    #region Initialization functions

    private void InitializeAtmosMonitoringConsole(EntityUid uid, AtmosMonitoringConsoleComponent component)
    {
        var xform = Transform(uid);

        if (xform.GridUid == null)
            return;

        var grid = GetSelectedMonitoringGrid(uid, xform); // Pirate: multiz

        if (!TryComp<MapGridComponent>(grid, out var map))
            return;

        UpdateAtmosMonitoringConsoleGridData(uid, component, grid); // Pirate: multiz
    }

    private void InitializeAtmosMonitoringDevice(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component)
    {
        // Rebuild tile
        var xform = Transform(uid);
        var gridUid = xform.GridUid;

        if (gridUid != null && TryComp<MapGridComponent>(gridUid, out var grid))
            RebuildSingleTileOfPipeNetwork(gridUid.Value, grid, xform.Coordinates);

        // Update blips on affected consoles
        if (component.NavMapBlip == null)
            return;

        var netEntity = GetNetEntity(uid);
        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            var isDirty = entConsole.AtmosDevices.Remove(netEntity);

            if (gridUid != null &&
                gridUid == GetSelectedMonitoringGrid(ent, entXform) && // Pirate: multiz
                xform.Anchored &&
                TryGetAtmosDeviceNavMapData(uid, component, xform, gridUid.Value, out var data))
            {
                entConsole.AtmosDevices.Add(netEntity, data.Value);
                isDirty = true;
            }

            if (isDirty)
                Dirty(ent, entConsole);
        }
    }

    private void ShutDownAtmosMonitoringEntity(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component)
    {
        // Rebuild tile
        var xform = Transform(uid);
        var gridUid = xform.GridUid;

        if (gridUid != null && TryComp<MapGridComponent>(gridUid, out var grid))
            RebuildSingleTileOfPipeNetwork(gridUid.Value, grid, xform.Coordinates);

        // Update blips on affected consoles
        if (component.NavMapBlip == null)
            return;

        var netEntity = GetNetEntity(uid);
        var query = AllEntityQuery<AtmosMonitoringConsoleComponent>();

        while (query.MoveNext(out var ent, out var entConsole))
        {
            if (entConsole.AtmosDevices.Remove(netEntity))
                Dirty(ent, entConsole);
        }
    }

    #endregion

    private int GetTileIndex(Vector2i relativeTile)
    {
        return relativeTile.X * ChunkSize + relativeTile.Y;
    }
}