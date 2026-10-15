// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Chimera;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Chimera;

[TestFixture]
public sealed class ChimeraIntegrationTest
{
    private static readonly ProtoId<NpcFactionPrototype> ChimeraFaction = "Chimera";

    [TestCase("Blunt", 3)]
    [TestCase("Heat", 14)]
    public async Task BodyDamageUsesChimeraResistance(string damageType, int expectedDamage)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var type = server.ProtoMan.Index<DamageTypePrototype>(damageType);
            var mob = entMan.SpawnEntity("MobLetoferolHorror", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;
            entMan.GetComponent<WoundableComponent>(chest).AllowWounds = false;

            var applied = damageable.ChangeDamage(mob,
                new DamageSpecifier(type, 10),
                targetPart: TargetBodyPart.Chest,
                canMiss: false);

            Assert.That(applied.DamageDict[type.ID], Is.EqualTo(FixedPoint2.New(expectedDamage)));
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("NanoTrasen")]
    [TestCase("Syndicate")]
    [TestCase("SimpleHostile")]
    [TestCase("AllHostile")]
    public async Task ExistingFactionsAcquireChimeraTargets(string faction)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var factions = server.System<NpcFactionSystem>();
            var npc = server.EntMan.SpawnEntity(null, map.GridCoords);
            var chimera = server.EntMan.SpawnEntity(null, map.GridCoords);
            factions.AddFaction(npc, faction);
            factions.AddFaction(chimera, ChimeraFaction);

            Assert.That(factions.GetNearbyHostiles(npc, 5), Does.Contain(chimera));
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpikeGlandKeepsGunAttachedAndCleansUpItsAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var actions = server.System<SharedActionsSystem>();
        var transforms = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        EntityUid mob = default;
        EntityUid groin = default;
        EntityUid gland = default;
        EntityUid oldAction = default;
        EntityUid oldGun = default;

        await server.WaitPost(() => mob = entMan.SpawnEntity("MobLetoferolHorror", map.GridCoords));
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            groin = body.GetBodyChildrenOfType(mob, BodyPartType.Groin).Single().Id;
            gland = body.GetPartOrgans(groin).Single(organ => organ.Component.SlotId == "liver").Id;
            var ability = entMan.GetComponent<ActionGunComponent>(mob);
            Assert.That(ability.ActionEntity, Is.Not.Null);
            Assert.That(ability.Gun, Is.Not.Null);
            oldAction = ability.ActionEntity!.Value;
            oldGun = ability.Gun!.Value;
            Assert.That(server.Transform(oldGun).ParentUid, Is.EqualTo(mob));

            transforms.SetCoordinates(mob, new EntityCoordinates(map.GridCoords.EntityId, new Vector2(2, 0)));
            Assert.That(transforms.GetWorldPosition(oldGun), Is.EqualTo(transforms.GetWorldPosition(mob)));
            Assert.That(body.RemoveOrgan(gland), Is.True);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActionGunComponent>(mob), Is.False);
            Assert.That(actions.GetActions(mob).Any(action => action.Owner == oldAction), Is.False);
            Assert.That(entMan.EntityExists(oldAction), Is.False);
            Assert.That(entMan.EntityExists(oldGun), Is.False);
            Assert.That(body.InsertOrgan(groin, gland, "liver"), Is.True);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var ability = entMan.GetComponent<ActionGunComponent>(mob);
            Assert.That(ability.ActionEntity, Is.Not.Null.And.Not.EqualTo(oldAction));
            Assert.That(ability.Gun, Is.Not.Null.And.Not.EqualTo(oldGun));
            Assert.That(server.Transform(ability.Gun!.Value).ParentUid, Is.EqualTo(mob));
            Assert.That(actions.GetActions(mob).Count(action =>
                entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == "ActionLetoferolSpike"),
                Is.EqualTo(1));
        });

        await server.WaitPost(() => entMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }
}
