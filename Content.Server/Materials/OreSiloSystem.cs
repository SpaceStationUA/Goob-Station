using System.Numerics; // Pirate: multiz
using Content.Server.Pinpointer;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems; // Pirate: multiz
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Player;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!; // Pirate: multiz
    [Dependency] private readonly CESharedZLevelsSystem _zLevelsServer = default!; // Pirate: multiz

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    #region Pirate: multiz - resolve map-time silo networks (same deck immediately, cross-deck once the Z-network forms)
    // Clients whose keyed silo isn't reachable yet (their deck hasn't been linked into the Z-network).
    // Retried in Update until linked or the attempt budget runs out. Can't use CEMultizLinkedGridPeersChangedEvent
    // as a trigger - that directed subscription is already owned by CEMultizCableHubSystem (one subscriber per pair).
    private readonly List<(EntityUid Uid, int Attempts)> _pendingAutoLink = new();
    private const int MaxAutoLinkAttempts = 30; // ~30s at the 1s cadence below
    private float _autoLinkTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreSiloClientComponent, MapInitEvent>(OnClientMapInit);
    }

    private void OnClientMapInit(Entity<OreSiloClientComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.SiloNetwork))
            return;

        // Same-deck (and single-map) links resolve now; cross-deck ones wait for the Z-network to form.
        if (!TryAutoLinkClient(ent))
            _pendingAutoLink.Add((ent.Owner, 0));
    }

    private void UpdatePendingAutoLinks(float frameTime)
    {
        if (_pendingAutoLink.Count == 0)
            return;

        _autoLinkTimer += frameTime;
        if (_autoLinkTimer < 1f)
            return;
        _autoLinkTimer = 0f;

        for (var i = _pendingAutoLink.Count - 1; i >= 0; i--)
        {
            var (uid, attempts) = _pendingAutoLink[i];
            if (TerminatingOrDeleted(uid)
                || !TryComp<OreSiloClientComponent>(uid, out var comp)
                || comp.Silo != null
                || TryAutoLinkClient((uid, comp))
                || attempts + 1 >= MaxAutoLinkAttempts)
            {
                _pendingAutoLink.RemoveAt(i);
            }
            else
            {
                _pendingAutoLink[i] = (uid, attempts + 1);
            }
        }
    }
    #endregion

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Sneakily uses override with TComponent parameter
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _clientLookup);

        #region Pirate: multiz - include clients on linked decks
        var siloGrid = _xform.GetGrid(ent.Owner);
        if (siloGrid is { } sg)
        {
            // Reproject the silo's local footprint onto each aligned peer grid.
            var siloLocal = Vector2.Transform(_xform.GetWorldPosition(ent.Owner), _xform.GetInvWorldMatrix(sg));
            foreach (var linkedGrid in _zLevelsServer.GetLinkedGrids(sg))
            {
                if (linkedGrid == sg || !TryComp<TransformComponent>(linkedGrid, out var gridXform))
                    continue;

                var peerWorld = Vector2.Transform(siloLocal, _xform.GetWorldMatrix(linkedGrid));
                _entityLookup.GetEntitiesInRange(gridXform.MapID, peerWorld, ent.Comp.Range, _clientLookup);
            }
        }
        #endregion

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Pirate: don't show clients that the server will reject.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdatePendingAutoLinks(frameTime); // Pirate: multiz

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actorComp, out var actorXform))
        {
            _silosToAdd.Clear();
            _silosToRemove.Clear();

            var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
            while (clientQuery.MoveNext(out _, out var clientComp, out var clientXform))
            {
                if (clientComp.Silo == null)
                    continue;

                // We limit it to same-grid checks only for peak perf
                if (actorXform.GridUid != clientXform.GridUid)
                    continue;

                if ((actorXform.LocalPosition - clientXform.LocalPosition).LengthSquared() <= OreSiloPreloadRangeSquared)
                {
                    _silosToAdd.Add(clientComp.Silo.Value);
                }
                else
                {
                    _silosToRemove.Add(clientComp.Silo.Value);
                }
            }

            foreach (var toRemove in _silosToRemove)
            {
                _pvsOverride.RemoveSessionOverride(toRemove, actorComp.PlayerSession);
            }
            foreach (var toAdd in _silosToAdd)
            {
                _pvsOverride.AddSessionOverride(toAdd, actorComp.PlayerSession);
            }
        }
    }
}
