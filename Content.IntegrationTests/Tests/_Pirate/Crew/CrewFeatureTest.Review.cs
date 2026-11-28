// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client._Pirate.StationAi;
using Content.Goobstation.Common.CCVar;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared._Pirate.Lavaland.Puzzle;
using Content.Shared._Pirate.StatusEffectNew;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Crew;

public sealed partial class CrewFeatureTest
{
    [Test]
    public async Task OccupiedSuitStoragePreventsPrisonDeployment()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var side = new Vector2i(1, 0);
            maps.SetTile(map.Grid, side, map.Tile.Tile);
            var locker = em.SpawnAttachedTo("SuitStorageBase", maps.GridTileToLocal(map.Grid, map.Grid.Comp, side));
            server.System<SharedTransformSystem>().AnchorEntity(locker, em.GetComponent<TransformComponent>(locker));
            var occupant = em.SpawnAttachedTo("MobHuman", map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            var storage = containers.GetContainer(locker, "entity_storage");
            Assert.That(containers.Insert(occupant, storage), Is.True);
            var actor = em.SpawnAttachedTo("MobHuman", map.GridCoords);
            var target = em.SpawnAttachedTo("MobHuman", map.GridCoords);
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Critical);
            var cube = em.SpawnAttachedTo("LavalandPrisonCube", map.GridCoords);
            var use = new AfterInteractEvent(actor, cube, target, map.GridCoords, true);
            em.EventBus.RaiseLocalEvent(cube, use);

            Assert.That(use.Handled, Is.False);
            Assert.That(storage.Contains(occupant), Is.True);
            Assert.That(em.EntityExists(occupant) && em.EntityExists(locker) && em.EntityExists(cube), Is.True);
            Assert.That(em.HasComponent<GodmodeComponent>(target) || em.HasComponent<BlockMovementComponent>(target), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DismantlingSpacePrisonPreservesBystandersAndConstruction(bool extended)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        var xf = server.System<SharedTransformSystem>();
        var maps = server.System<SharedMapSystem>();
        EntityUid grid = default, bystander = default, item = default, captive = default;
        MapCoordinates standing = default;
        await server.WaitAssertion(() =>
        {
            var space = new EntityCoordinates(map.MapUid, new Vector2(90, 90));
            var actor = em.SpawnAttachedTo("MobHuman", space);
            captive = em.SpawnAttachedTo("MobHuman", space);
            server.System<MobStateSystem>().ChangeMobState(captive, MobState.Critical);
            var cube = em.SpawnAttachedTo("LavalandPrisonCube", space);
            var use = new AfterInteractEvent(actor, cube, captive, space, true);
            em.EventBus.RaiseLocalEvent(cube, use);
            Assert.That(use.Handled, Is.True);
            var puzzle = em.AllComponentsList<LavalandSlidingPuzzleComponent>().Single().Component;
            grid = puzzle.GeneratedGrid!.Value;
            var gc = em.GetComponent<MapGridComponent>(grid);
            var floor = maps.GridTileToLocal(grid, gc, Vector2i.Zero);
            bystander = em.SpawnAttachedTo("MobHuman", floor);
            standing = xf.GetMapCoordinates(bystander);
            var side = new Vector2i(2, 0);
            if (extended)
                maps.SetTile(grid, gc, side, map.Tile.Tile);
            item = em.SpawnAttachedTo("Crowbar", extended ? maps.GridTileToLocal(grid, gc, side) : floor);
            em.DeleteEntity(puzzle.Elements[0]);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(em.EntityExists(grid), Is.EqualTo(extended));
            Assert.That(em.EntityExists(bystander) && em.EntityExists(item) && em.EntityExists(captive), Is.True);
            Assert.That(xf.GetMapCoordinates(bystander).InRange(standing, 0.25f), Is.True);
            Assert.That(em.GetComponent<TransformComponent>(item).ParentUid, Is.EqualTo(extended ? grid : map.MapUid));
            Assert.That(em.HasComponent<GodmodeComponent>(captive), Is.False);
            if (extended)
                Assert.That(maps.GetAllTiles(grid, em.GetComponent<MapGridComponent>(grid)).Count(), Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NaturalRuinHasBasaltPerimeterAndSavedPillarsCanSlide()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var loader = server.System<MapLoaderSystem>();
            maps.CreateMap(out var mapId);
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var pool = prototypes.Index<LavalandRuinPoolPrototype>("LavalandRuins");
            var ruin = prototypes.Index<LavalandGridRuinPrototype>("LavalandSlidingPuzzleRuin");
            Assert.That(pool.GridRuins[ruin.ID], Is.EqualTo(3));
            Assert.That(loader.TryLoadGrid(mapId, ruin.Path, out var room), Is.True);
            var grid = room!.Value;
            var basalt = server.ResolveDependency<ITileDefinitionManager>()["FloorBasalt"].TileId;
            var tiles = maps.GetAllTiles(grid, grid.Comp).ToArray();
            Assert.That(tiles.Length, Is.EqualTo(25));
            Assert.That(tiles.All(tile => tile.Tile.TypeId == basalt), Is.True);

            var path = new ResPath("/pirate-puzzle-roundtrip.yml");
            Assert.That(loader.TrySaveGrid(grid, path), Is.True);
            try
            {
                maps.CreateMap(out var nextMap);
                Assert.That(loader.TryLoadGrid(nextMap, path, out var restored), Is.True);
                var next = restored!.Value;
                var (marker, board) = em.AllComponentsList<LavalandSlidingPuzzleComponent>()
                    .Single(b => em.GetComponent<TransformComponent>(b.Uid).GridUid == next.Owner);
                Assert.That(board.Elements, Has.Count.EqualTo(8));
                var actor = em.SpawnAttachedTo("MobHuman", em.GetComponent<TransformComponent>(marker).Coordinates);
                var parts = board.Elements.Select(em.GetComponent<LavalandSlidingPuzzleElementComponent>).ToArray();
                Assert.That(parts.All(part => part.Source == marker), Is.True);
                Assert.That(parts.Select(part => part.Id), Is.EquivalentTo(Enumerable.Range(1, 9).Except([board.EmptyTileId])));
                var moved = false;
                foreach (var pillar in board.Elements)
                {
                    var position = em.GetComponent<TransformComponent>(pillar).Coordinates;
                    var interact = new InteractHandEvent(actor, pillar);
                    em.EventBus.RaiseLocalEvent(pillar, interact);
                    if (!interact.Handled)
                        continue;
                    moved = em.GetComponent<TransformComponent>(pillar).Coordinates != position;
                    break;
                }
                Assert.That(moved, Is.True, "A restored pillar must respond to a legal slide.");
            }
            finally
            {
                server.ResolveDependency<IResourceManager>().UserData.Delete(path);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SamePolarityContactKnocksDownBothCrew()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid a = default, b = default;
        await server.WaitPost(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var side = new Vector2i(1, 0);
            maps.SetTile(map.Grid, side, map.Tile.Tile);
            var gravity = em.EnsureComponent<Content.Shared.Gravity.GravityComponent>(map.Grid);
            server.System<Content.Server.Gravity.GravitySystem>().EnableGravity(map.Grid, gravity);
            a = em.SpawnAttachedTo("MobHuman", map.GridCoords);
            b = em.SpawnAttachedTo("MobHuman", maps.GridTileToLocal(map.Grid, map.Grid.Comp, side));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var effects = server.System<StatusEffectsSystem>();
            BiomagneticPolarizationStatusEffectComponent Charge(EntityUid target)
            {
                Assert.That(effects.TryAddStatusEffectDuration(target, SharedBiomagneticPolarizationSystem.BiomagEffectID,
                    out var effect, TimeSpan.FromMinutes(1)), Is.True);
                var charge = em.GetComponent<BiomagneticPolarizationStatusEffectComponent>(effect!.Value);
                charge.Polarization = true;
                return charge;
            }
            var first = Charge(a);
            var second = Charge(b);
            var touch = new InteractionSuccessEvent(b);
            em.EventBus.RaiseLocalEvent(a, ref touch);
            Assert.That(em.HasComponent<KnockedDownComponent>(a) && em.HasComponent<KnockedDownComponent>(b), Is.True);
            Assert.That(first.CooldownEnd, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(first.CooldownEnd, Is.EqualTo(second.CooldownEnd));
            Assert.That(first.Expired || second.Expired, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CascadeHoldsDockedShuttleAndScheduledRoundEnd(bool alreadyDeparted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, DummyTicker = false });
        var server = pair.Server;
        var shuttle = server.System<EmergencyShuttleSystem>();
        var ticker = server.System<GameTicker>();
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, true);
            shuttle.DockEmergencyShuttle();
            Assert.That(shuttle.EmergencyShuttleArrived, Is.True);
            shuttle._consoleAccumulator = alreadyDeparted ? 0f : 1f;
            shuttle.TransitTime = 1f;
            if (alreadyDeparted)
            {
                shuttle.Update(0.1f);
                Assert.That(shuttle.ShuttlesLeft, Is.True);
            }
            Assert.That(ticker.StartGameRule("SupermatterCascadeDelamRule"), Is.True);
        });
        await pair.RunSeconds(4);
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(shuttle.ShuttlesLeft, Is.EqualTo(alreadyDeparted));
            if (!alreadyDeparted)
                Assert.That(shuttle._consoleAccumulator, Is.EqualTo(1f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AiLinkKeepsCustomRadioTintAndPartialNameHighlight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            client.CfgMan.SetCVar(GoobCVars.UseCustomRadioChannelColors, true);
            client.CfgMan.SetCVar(GoobCVars.RadioChannelColors, "Common=#123456");
            var chat = new ChatMessage(ChatChannel.Radio, "Привіт",
                "[airadiolink target=\"123\" channel=\"Common\"]Ірина Коваль[/airadiolink] каже", default, null);
            var highlighted = SharedChatSystem.InjectTagAroundString(chat, "Ірина", "color", "#ff0055");
            Assert.That(highlighted, Is.Not.EqualTo(chat.WrappedMessage));
            var formatted = AiRadioSpeakerTag.CreateLinks(FormattedMessage.FromMarkupOrThrow(highlighted));
            var tag = client.ResolveDependency<MarkupTagManager>().GetMarkupTagHandler("airadiospeaker")!;
            var name = formatted.Where(node => node.Name == "airadiospeaker" && !node.Closing)
                .Select(node =>
                {
                    Assert.That(tag.TryCreateControl(node, out var control), Is.True);
                    return (Label) control!;
                }).ToArray();
            Assert.That(name.Select(label => label.Text), Is.EqualTo(new[] { "Ірина", " Коваль" }));
            Assert.That(name[0].FontColorOverride, Is.EqualTo(Color.FromHex("#ff0055")));
            Assert.That(name[1].FontColorOverride, Is.EqualTo(Color.FromHex("#123456")));
        });
        await pair.CleanReturnAsync();
    }
}
