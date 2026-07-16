using System.Numerics; // Pirate: multiz
using Content.Shared._Pirate.ZLevels.Core.EntitySystems; // Pirate: multiz
using Content.Shared.Power.EntitySystems;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Materials.OreSilo;

public abstract class SharedOreSiloSystem : EntitySystem
{
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!; // Pirate: multiz - link across z-levels

    private EntityQuery<OreSiloClientComponent> _clientQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<OreSiloComponent, ToggleOreSiloClientMessage>(OnToggleOreSiloClient);
        SubscribeLocalEvent<OreSiloComponent, ComponentShutdown>(OnSiloShutdown);
        Subs.BuiEvents<OreSiloComponent>(OreSiloUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBoundUIOpened);
        });


        SubscribeLocalEvent<OreSiloClientComponent, GetStoredMaterialsEvent>(OnGetStoredMaterials);
        SubscribeLocalEvent<OreSiloClientComponent, ConsumeStoredMaterialsEvent>(OnConsumeStoredMaterials);
        SubscribeLocalEvent<OreSiloClientComponent, ComponentShutdown>(OnClientShutdown);

        _clientQuery = GetEntityQuery<OreSiloClientComponent>();
    }

    private void OnToggleOreSiloClient(Entity<OreSiloComponent> ent, ref ToggleOreSiloClientMessage args)
    {
        var client = GetEntity(args.Client);

        if (!_clientQuery.TryComp(client, out var clientComp))
        {
            PopupLinkFailure(ent, args.Actor, OreSiloLinkResult.Unavailable);
            return;
        }

        if (ent.Comp.Clients.Contains(client)) // remove client
        {
            clientComp.Silo = null;
            Dirty(client, clientComp);
            ent.Comp.Clients.Remove(client);
            Dirty(ent);

            UpdateOreSiloUi(ent);
        }
        else // add client
        {
            var result = GetLinkResult((ent, ent, Transform(ent)), client);
            if (result != OreSiloLinkResult.Success)
            {
                PopupLinkFailure(ent, args.Actor, result);
                return;
            }

            var clientMats = _materialStorage.GetStoredMaterials(client, true);
            var inverseMats = new Dictionary<string, int>();
            foreach (var (mat, amount) in clientMats)
            {
                inverseMats.Add(mat, -amount);
            }
            _materialStorage.TryChangeMaterialAmount(client, inverseMats, localOnly: true);
            _materialStorage.TryChangeMaterialAmount(ent.Owner, clientMats);

            ent.Comp.Clients.Add(client);
            Dirty(ent);
            clientComp.Silo = ent;
            Dirty(client, clientComp);

            UpdateOreSiloUi(ent);
        }
    }

    private void OnBoundUIOpened(Entity<OreSiloComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateOreSiloUi(ent);
    }

    private void OnSiloShutdown(Entity<OreSiloComponent> ent, ref ComponentShutdown args)
    {
        foreach (var client in ent.Comp.Clients)
        {
            if (!_clientQuery.TryComp(client, out var comp))
                continue;

            comp.Silo = null;
            Dirty(client, comp);
        }
    }

    protected virtual void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {

    }

    private void OnGetStoredMaterials(Entity<OreSiloClientComponent> ent, ref GetStoredMaterialsEvent args)
    {
        if (args.LocalOnly)
            return;

        if (ent.Comp.Silo is not { } silo)
            return;

        if (!CanTransmitMaterials(silo, ent))
            return;

        var materials = _materialStorage.GetStoredMaterials(silo);

        foreach (var (mat, amount) in materials)
        {
            // Don't supply materials that they don't usually have access to.
            if (!_materialStorage.IsMaterialWhitelisted((args.Entity, args.Entity), mat))
                continue;

            var existing = args.Materials.GetOrNew(mat);
            args.Materials[mat] = existing + amount;
        }
    }

    private void OnConsumeStoredMaterials(Entity<OreSiloClientComponent> ent, ref ConsumeStoredMaterialsEvent args)
    {
        if (args.LocalOnly)
            return;

        if (ent.Comp.Silo is not { } silo || !TryComp<MaterialStorageComponent>(silo, out var materialStorage))
            return;

        if (!CanTransmitMaterials(silo, ent))
            return;

        foreach (var (mat, amount) in args.Materials)
        {
            if (!_materialStorage.TryChangeMaterialAmount(silo, mat, amount, materialStorage))
                continue;
            args.Materials[mat] = 0;
        }
    }

    private void OnClientShutdown(Entity<OreSiloClientComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<OreSiloComponent>(ent.Comp.Silo, out var silo))
            return;

        silo.Clients.Remove(ent);
        Dirty(ent.Comp.Silo.Value, silo);
        UpdateOreSiloUi((ent.Comp.Silo.Value, silo));
    }

    /// <summary>
    /// Checks if a given client fulfills the criteria to link/receive materials from an ore silo.
    /// </summary>
    [PublicAPI]
    public bool CanTransmitMaterials(Entity<OreSiloComponent?, TransformComponent?> silo, EntityUid client)
    {
        return GetLinkResult(silo, client) == OreSiloLinkResult.Success;
    }

    #region Pirate: multiz - auto-link keyed clients across linked decks
    /// <summary>Links an unlinked keyed client to a silo in the same z-network.</summary>
    /// <remarks>Ignores power and range because transmission validates both.</remarks>
    public bool TryAutoLinkClient(Entity<OreSiloClientComponent> client)
    {
        if (client.Comp.Silo != null || string.IsNullOrEmpty(client.Comp.SiloNetwork))
            return false;

        if (_transform.GetGrid(client.Owner) is not { } clientGrid)
            return false;

        var linkedGrids = _zLevels.GetLinkedGrids(clientGrid);

        var query = EntityQueryEnumerator<OreSiloComponent, TransformComponent>();
        while (query.MoveNext(out var siloUid, out var silo, out var siloXform))
        {
            if (silo.SiloNetwork != client.Comp.SiloNetwork)
                continue;
            if (siloXform.GridUid is not { } siloGrid || !linkedGrids.Contains(siloGrid))
                continue;

            silo.Clients.Add(client.Owner);
            Dirty(siloUid, silo);
            client.Comp.Silo = siloUid;
            Dirty(client.Owner, client.Comp);
            UpdateOreSiloUi((siloUid, silo));
            return true;
        }

        return false;
    }
    #endregion

    private OreSiloLinkResult GetLinkResult(Entity<OreSiloComponent?, TransformComponent?> silo, EntityUid client)
    {
        if (!Resolve(silo, ref silo.Comp1, ref silo.Comp2))
            return OreSiloLinkResult.Unavailable;

        if (!_powerReceiver.IsPowered(silo.Owner))
            return OreSiloLinkResult.Unpowered;

        var siloGrid = _transform.GetGrid(silo.Owner);
        var clientGrid = _transform.GetGrid(client);

        #region Pirate: multiz - link aligned peer grids
        if (siloGrid != clientGrid)
        {
            if (siloGrid is not { } sg || clientGrid is not { } cg || !_zLevels.AreGridsLinked(sg, cg))
                return OreSiloLinkResult.DifferentGrid;

            // Compare local positions because peer decks use different maps.
            if (!InFootprintRange(silo.Owner, client, sg, cg, silo.Comp1.Range))
                return OreSiloLinkResult.OutOfRange;

            return OreSiloLinkResult.Success;
        }
        #endregion

        if (!_transform.InRange((silo.Owner, silo.Comp2), client, silo.Comp1.Range))
            return OreSiloLinkResult.OutOfRange;

        return OreSiloLinkResult.Success;
    }

    // Pirate: multiz - compare horizontal positions across aligned peer grids.
    private bool InFootprintRange(EntityUid silo, EntityUid client, EntityUid siloGrid, EntityUid clientGrid, float range)
    {
        var siloLocal = Vector2.Transform(_transform.GetWorldPosition(silo), _transform.GetInvWorldMatrix(siloGrid));
        var clientLocal = Vector2.Transform(_transform.GetWorldPosition(client), _transform.GetInvWorldMatrix(clientGrid));
        return (siloLocal - clientLocal).LengthSquared() < range * range;
    }

    // Pirate: surface server-side link rejection instead of silently ignoring the click.
    private void PopupLinkFailure(EntityUid silo, EntityUid user, OreSiloLinkResult result)
    {
        if (!_net.IsServer)
            return;

        var message = result switch
        {
            OreSiloLinkResult.Unpowered => "ore-silo-ui-link-failed-unpowered",
            OreSiloLinkResult.DifferentGrid => "ore-silo-ui-link-failed-different-grid",
            OreSiloLinkResult.OutOfRange => "ore-silo-ui-link-failed-out-of-range",
            _ => "ore-silo-ui-link-failed-unavailable",
        };

        _popup.PopupClient(Loc.GetString(message), silo, user, PopupType.SmallCaution);
    }

    private enum OreSiloLinkResult : byte
    {
        Success,
        Unavailable,
        Unpowered,
        DifferentGrid,
        OutOfRange,
    }
}
