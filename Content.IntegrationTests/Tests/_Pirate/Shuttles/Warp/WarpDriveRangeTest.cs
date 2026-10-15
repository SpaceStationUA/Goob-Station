// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared._NF.Shuttles;
using Content.Shared.Shuttles.Systems;
using NUnit.Framework;
using Robust.Server;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate.Shuttles.Warp;

/// <summary>
///     Checks that a hull's reachable radius follows the warp drive's state.
/// </summary>
[TestFixture]
[TestOf(typeof(WarpDriveGridComponent))]
[TestOf(typeof(SharedShuttleSystem))]
public sealed class WarpDriveRangeTest : RobustIntegrationTest
{
    /// <summary>
    ///     The bluespace radius a hull gets from its drives, matching ftldrive.yml.
    /// </summary>
    private const float BluespaceRange = 512f;

    private const float WarpRange = 20_000f;

    [Test]
    public async Task IdleWarpDriveDoesNotExtendRange()
    {
        await RunRangeTest((Active: false, Expected: BluespaceRange));
    }

    [Test]
    public async Task EngagedWarpDriveOverridesRange()
    {
        await RunRangeTest((Active: true, Expected: WarpRange));
    }

    [Test]
    public async Task WarpShutdownRestoresBluespaceRange()
    {
        await RunRangeTest(
            (Active: true, Expected: WarpRange),
            (Active: false, Expected: BluespaceRange));
    }

    /// <summary>
    ///     A grid without a warp drive is untouched. Guards against the range hook applying to every
    ///     shuttle in the game.
    /// </summary>
    [Test]
    public async Task GridWithoutWarpDriveIsUnaffected()
    {
        using var server = StartServer(CreateOptions());
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var shuttle = entMan.System<SharedShuttleSystem>();

            var grid = SpawnGridWithBluespaceDrive(entMan);

            Assert.That(shuttle.GetFTLRange(grid), Is.EqualTo(BluespaceRange));
        });
    }

    /// <summary>
    ///     Runs each step in order: sets the drive's state and checks the range it grants.
    /// </summary>
    private async Task RunRangeTest(params (bool Active, float Expected)[] steps)
    {
        using var server = StartServer(CreateOptions());
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var shuttle = entMan.System<SharedShuttleSystem>();

            var grid = SpawnGridWithBluespaceDrive(entMan);
            var warp = entMan.AddComponent<WarpDriveGridComponent>(grid);

            foreach (var (active, expected) in steps)
            {
                warp.Active = active;
                warp.Range = active ? WarpRange : 0f;

                Assert.That(shuttle.GetFTLRange(grid), Is.EqualTo(expected));
            }
        });
    }

    private static ServerIntegrationOptions CreateOptions()
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

        return options;
    }

    private static EntityUid SpawnGridWithBluespaceDrive(IEntityManager entMan)
    {
        var mapSystem = entMan.System<SharedMapSystem>();
        mapSystem.CreateMap(out var mapId);

        // CreateGridEntity parents the grid to the map, which is all GetFTLRange needs.
        var grid = mapSystem.CreateGridEntity(mapId);

        // CreateGridEntity already adds an FTLDriveComponent, so this sets that one up instead of adding another.
        var ftl = entMan.GetComponent<FTLDriveComponent>(grid.Owner);
        ftl.Data = new FTLDriveData(BluespaceRange, true);

        return grid.Owner;
    }
}
