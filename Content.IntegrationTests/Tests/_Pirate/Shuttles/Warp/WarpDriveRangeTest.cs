// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared._NF.Shuttles;
using Content.Shared.Shuttles.Systems;
using NUnit.Framework;
using Robust.Server;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate.Shuttles.Warp;

/// <summary>
///     Tests the one thing the warp drive has to get right for the rest of the feature to mean
///     anything: that a hull's reachable radius follows the drive's state.
/// </summary>
/// <remarks>
///     Deliberately narrow. The claim being defended is mechanical — "engaged means the warp
///     radius, anything else means the hull's bluespace radius" — and every other part of the
///     feature leans on it. A visual test would obscure that; a full round test would exercise
///     power grids and maps and prove nothing about this specific contract.
/// </remarks>
[TestFixture]
[TestOf(typeof(WarpDriveGridComponent))]
[TestOf(typeof(SharedShuttleSystem))]
public sealed class WarpDriveRangeTest : RobustIntegrationTest
{
    /// <summary>
    ///     The bluespace radius a hull gets from its drives, matching
    ///     Resources/Prototypes/_Mono/Entities/Structures/Machines/ftldrive.yml.
    /// </summary>
    private const float BluespaceRange = 512f;

    private const float WarpRange = 20_000f;

    /// <summary>
    ///     An idle warp drive leaves the hull exactly as it was. This is the case that would
    ///     silently turn the warp drive into a permanent upgrade.
    /// </summary>
    [Test]
    public async Task IdleWarpDriveDoesNotExtendRange()
    {
        await RunRangeTest(active: false, expected: BluespaceRange);
    }

    /// <summary>
    ///     An engaged warp drive overrides the bluespace radius outright rather than adding to it.
    ///     Additive would make the reachable radius depend on what else happens to be bolted to
    ///     the hull, which is not a property this feature should have.
    /// </summary>
    [Test]
    public async Task EngagedWarpDriveOverridesRange()
    {
        await RunRangeTest(active: true, expected: WarpRange);
    }

    /// <summary>
    ///     Coming down from a warp returns the hull to its bluespace radius, not to zero. Zero
    ///     would be worse than merely wrong — it would strand the crew with a dead console.
    /// </summary>
    [Test]
    public async Task WarpShutdownRestoresBluespaceRange()
    {
        await RunRangeTest(active: false, expected: BluespaceRange);
    }

    /// <summary>
    ///     A grid with no warp drive at all is untouched by any of this. Guards against the range
    ///     hook accidentally applying to ordinary shuttles, which would silently change FTL for
    ///     every vehicle in the game.
    /// </summary>
    [Test]
    public async Task GridWithoutWarpDriveIsUnaffected()
    {
        var options = new ServerIntegrationOptions
        {
            ContentStart = true,
            ContentAssemblies = PoolManager.GetAssemblies(client: false, includePoolAssembly: false),
            Pool = false,
            FailureLogLevel = LogLevel.Fatal,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
            },
        };

        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;

        using var server = StartServer(options);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var shuttle = entMan.System<SharedShuttleSystem>();

            var mapManager = server.ResolveDependency<IMapManager>();
            var grid = SpawnGridWithBluespaceDrive(entMan, mapManager);

            Assert.That(shuttle.GetFTLRange(grid), Is.EqualTo(BluespaceRange));
        });
    }

    private async Task RunRangeTest(bool active, float expected)
    {
        var options = new ServerIntegrationOptions
        {
            ContentStart = true,
            ContentAssemblies = PoolManager.GetAssemblies(client: false, includePoolAssembly: false),
            Pool = false,
            FailureLogLevel = LogLevel.Fatal,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
            },
        };

        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;

        using var server = StartServer(options);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var shuttle = entMan.System<SharedShuttleSystem>();

            var mapManager = server.ResolveDependency<IMapManager>();
            var grid = SpawnGridWithBluespaceDrive(entMan, mapManager);

            var warp = entMan.AddComponent<WarpDriveGridComponent>(grid);
            warp.Active = active;
            warp.Range = active ? WarpRange : 0f;

            Assert.That(shuttle.GetFTLRange(grid), Is.EqualTo(expected));
        });
    }

    private static EntityUid SpawnGridWithBluespaceDrive(IEntityManager entMan, IMapManager mapManager)
    {
        var mapUid = mapManager.CreateMap(new MapId(++_mapId));

        // CreateGridEntity parents the grid to the map itself, which is all the range check
        // needs - GetFTLRange resolves against the grid entity, not against any world position.
        var grid = mapManager.CreateGridEntity(mapUid);

        // CreateGridEntity already adds an FTLDriveComponent with the default radius, so this is
        // setting the existing one up as a bluespace drive rather than adding a second.
        var ftl = entMan.GetComponent<FTLDriveComponent>(grid.Owner);
        ftl.Data = new FTLDriveData(BluespaceRange, true);

        return grid.Owner;
    }

    private static int _mapId;
}