using System.Linq;
using Content.Goobstation.Common.Traitor;
using Content.Goobstation.Server.Traitor.PenSpin;
using Content.Goobstation.Shared.Traitor.PenSpin;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Traitor.Uplink;
using Content.Shared._Pirate.Reputation;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Goobstation;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class UplinkPreferenceTests
{
    private TestPair _pair = default!;
    private EntityUid _player;

    private static readonly ProtoId<UplinkPreferencePrototype> PenPreference = "UplinkPen";

    [SetUp]
    public async Task Setup()
    {
        _pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = _pair.Server;
        var ticker = server.System<GameTicker>();

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await _pair.RunTicksSync(10);

        _player = _pair.Player!.AttachedEntity!.Value;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _pair.CleanReturnAsync();
    }

    private async Task<EntityUid> SpawnPenInHand()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var handsSys = server.System<SharedHandsSystem>();

        EntityUid pen = default;
        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(_player).Coordinates;
            pen = entMan.SpawnEntity("Pen", coords);
            handsSys.TryPickupAnyHand(_player, pen);
        });
        await _pair.RunTicksSync(5);

        return pen;
    }

    [Test]
    public async Task TestFindPdaUplinkTarget()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var goobUplinkSys = server.System<GoobCommonUplinkSystem>();

        await server.WaitAssertion(() =>
        {
            var pdaTarget = goobUplinkSys.FindUplinkTarget(_player, new[] { "Pda" });
            Assert.That(pdaTarget, Is.Not.Null, "Player should have a PDA");
            Assert.That(entMan.HasComponent<PdaComponent>(pdaTarget!.Value), Is.True);
        });
    }

    [Test]
    public async Task TestFindPenUplinkTarget()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var goobUplinkSys = server.System<GoobCommonUplinkSystem>();

        await SpawnPenInHand();

        await server.WaitAssertion(() =>
        {
            var penTarget = goobUplinkSys.FindUplinkTarget(_player, new[] { "Pen" });
            Assert.That(penTarget, Is.Not.Null, "Player should have a pen");
            Assert.That(entMan.HasComponent<PenComponent>(penTarget!.Value), Is.True);
        });
    }

    [Test]
    public async Task TestPenUplinkCodeGeneration()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var uplinkSys = server.System<UplinkSystem>();
        var goobUplinkSys = server.System<GoobCommonUplinkSystem>();

        await SpawnPenInHand();

        await server.WaitPost(() => uplinkSys.AddUplink(_player, 20, PenPreference, out _, out _));
        await _pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var penTarget = goobUplinkSys.FindUplinkTarget(_player, new[] { "Pen" });
            Assert.That(penTarget, Is.Not.Null);

            var spinComp = entMan.GetComponent<PenComponent>(penTarget!.Value);
            Assert.That(spinComp.CombinationLength, Is.EqualTo(4));
            Assert.That(spinComp.MinDegree, Is.EqualTo(0));
            Assert.That(spinComp.MaxDegree, Is.EqualTo(359));

            var uplinkComp = entMan.GetComponent<PenSpinUplinkComponent>(penTarget.Value);
            Assert.That(uplinkComp.Code, Is.Not.Null);
            Assert.That(uplinkComp.Code!.Length, Is.EqualTo(4));

            foreach (var degree in uplinkComp.Code)
            {
                Assert.That(degree, Is.GreaterThanOrEqualTo(0));
                Assert.That(degree, Is.LessThanOrEqualTo(359));
            }
        });
    }

    [Test]
    public async Task TestAllUplinkPreferences()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var uplinkSys = server.System<UplinkSystem>();
        var handsSys = server.System<SharedHandsSystem>();

        foreach (var pref in protoMan.EnumeratePrototypes<UplinkPreferencePrototype>())
        {
            if (pref.SearchComponents != null)
            {
                await server.WaitPost(() =>
                {
                    if (Array.IndexOf(pref.SearchComponents, "Pen") >= 0)
                    {
                        var coords = entMan.GetComponent<TransformComponent>(_player).Coordinates;
                        var pen = entMan.SpawnEntity("Pen", coords);
                        handsSys.TryPickupAnyHand(_player, pen);
                    }
                });
                await _pair.RunTicksSync(5);
            }

            await server.WaitAssertion(() =>
            {
                var success = uplinkSys.AddUplink(_player, 20, pref.ID, out var uplinkTarget, out var setupEvent);
                Assert.That(success, Is.True, $"TryAddUplink failed for preference {pref.ID}");

                if (pref.SearchComponents != null)
                {
                    Assert.That(uplinkTarget, Is.Not.Null, $"Should find uplink target for {pref.ID}");
                    Assert.That(setupEvent, Is.Not.Null, $"SetupUplinkEvent should be raised for {pref.ID}");
                    Assert.That(setupEvent!.Value.Handled, Is.True, $"SetupUplinkEvent should be handled for {pref.ID}");

                    var store = entMan.GetComponent<StoreComponent>(uplinkTarget!.Value);
                    Assert.That(store.Balance.ContainsKey("Telecrystal"), Is.True, $"Store should have TC for {pref.ID}");
                    Assert.That((int) store.Balance["Telecrystal"], Is.EqualTo(20), $"Store should have 20 TC for {pref.ID}");
                }
                else // Fallback
                {
                    Assert.That(uplinkTarget, Is.Null, $"Implant preference {pref.ID} should have no target entity");
                    Assert.That(setupEvent, Is.Null, $"Implant preference {pref.ID} should have no setup event");
                }
            });
        }
    }

    // Pirate: creating a preferred uplink must not attach its contracts to an unrelated PDA.
    [TestCase("UplinkPen")]
    [TestCase("UplinkPda")]
    [TestCase("UplinkImplant")]
    public async Task TestTraitorContractsUsePreferredUplink(string preference)
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var prefs = server.ResolveDependency<IServerPreferencesManager>();
        var user = _pair.Player!.UserId;
        var original = prefs.GetPreferences(user);
        var slot = original.SelectedCharacterIndex;
        var profile = (HumanoidCharacterProfile) original.SelectedCharacter;
        var loadout = new RoleLoadout("AntagTraitor");
        loadout.SelectedLoadouts["TraitorUplink"] =
            [new Loadout { Prototype = server.ProtoMan.Index<UplinkPreferencePrototype>(preference).Loadout! }];

        await SpawnPenInHand();
        EntityUid target = default;
        EntityUid mind = default;

        try
        {
            await server.WaitPost(() => prefs.SetProfile(user, slot, profile.WithLoadout(loadout)).Wait());
            await server.WaitAssertion(() =>
            {
                var uplinks = server.System<GoobCommonUplinkSystem>();
                var pda = uplinks.FindUplinkTarget(_player, ["Pda"])!.Value;
                var pen = uplinks.FindUplinkTarget(_player, ["Pen"])!.Value;
                Assert.That(server.System<TraitorRuleSystem>().MakeTraitor(_player,
                    new TraitorRuleComponent()), Is.True);

                mind = server.System<MindSystem>().GetMind(_player)!.Value;
                target = preference switch
                {
                    "UplinkPen" => pen,
                    "UplinkPda" => pda,
                    _ => entMan.GetComponent<ImplantedComponent>(_player).ImplantContainer.ContainedEntities
                        .Single(entity => entMan.HasComponent<StoreComponent>(entity)),
                };

                Assert.That(entMan.GetComponent<StoreContractsComponent>(target).Mind, Is.EqualTo(mind),
                    "The contract hub must belong to the selected uplink's traitor.");
                Assert.That(entMan.GetComponent<ContractsComponent>(mind).Stores, Is.EquivalentTo(new[] { target }),
                    "An unrelated PDA must not capture the preferred uplink's contracts or rewards.");
                if (target != pda && entMan.TryGetComponent<StoreContractsComponent>(pda, out var pdaContracts))
                    Assert.That(pdaContracts.Mind, Is.Null);

                server.System<ReputationSystem>().ToggleUI(_player, target);
                Assert.That(server.System<SharedUserInterfaceSystem>()
                    .TryGetUiState<ContractsState>(target, ContractsUiKey.Key, out _), Is.True,
                    "Opening the selected uplink must supply its contract hub state.");
            });

            await _pair.RunTicksSync(5);
            var clientTarget = _pair.ToClientUid(target);
            var clientMind = _pair.ToClientUid(mind);
            await _pair.Client.WaitAssertion(() =>
                Assert.That(_pair.Client.EntMan.GetComponent<StoreContractsComponent>(clientTarget).Mind,
                    Is.EqualTo(clientMind), "The client needs the contract owner to expose the pen's contract button."));
        }
        finally
        {
            await server.WaitPost(() => prefs.SetProfile(user, slot, profile).Wait());
        }
    }

    [Test]
    public async Task TestImplantUplinkBalance()
    {
        var server = _pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var uplinkSys = server.System<UplinkSystem>();

        const int startingBalance = 100;
        var implantPreference = new ProtoId<UplinkPreferencePrototype>("UplinkImplant");

        await server.WaitAssertion(() =>
        {
            var success = uplinkSys.AddUplink(_player, startingBalance, implantPreference, out var uplinkTarget, out _);
            Assert.That(success, Is.True, "Implant uplink should succeed");
            Assert.That(uplinkTarget, Is.Null, "Implant preference should not return an uplink target entity");

            Assert.That(entMan.TryGetComponent<ImplantedComponent>(_player, out var implanted), Is.True,
                "Player should have ImplantedComponent after implant uplink");

            EntityUid? implantStore = null;
            foreach (var implantEnt in implanted!.ImplantContainer.ContainedEntities)
            {
                if (entMan.HasComponent<StoreComponent>(implantEnt))
                {
                    implantStore = implantEnt;
                    break;
                }
            }

            Assert.That(implantStore, Is.Not.Null, "Should find a store component on the implant");

            var store = entMan.GetComponent<StoreComponent>(implantStore!.Value);
            Assert.That(store.Balance.ContainsKey("Telecrystal"), Is.True);

            var catalog = protoMan.Index<ListingPrototype>("UplinkUplinkImplanter");
            var implantCost = (int) catalog.Cost["Telecrystal"];
            var expectedBalance = startingBalance - implantCost;
            Assert.That((int) store.Balance["Telecrystal"], Is.EqualTo(expectedBalance),
                $"Implant store should have {expectedBalance} TC (starting {startingBalance} minus {implantCost} implant cost)");
        });
    }
}

[TestFixture]
public sealed class UplinkFallbackTests
{
    private static readonly ProtoId<UplinkPreferencePrototype> PenPreference = "UplinkPen";

    [Test]
    public async Task TestAddUplinkFallbackToImplant()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var uplinkSys = server.System<UplinkSystem>();
        var goobUplinkSys = server.System<GoobCommonUplinkSystem>();

        EntityUid dummy = default;
        await server.WaitPost(() =>
        {
            dummy = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var penTarget = goobUplinkSys.FindUplinkTarget(dummy, new[] { "Pen" });
            Assert.That(penTarget, Is.Null, "Dummy should not have a pen");

            var success = uplinkSys.AddUplink(dummy, 20, PenPreference, out _, out _);
            Assert.That(success, Is.True, "Should fall back to implant when pen unavailable");
        });

        await pair.CleanReturnAsync();
    }
}
