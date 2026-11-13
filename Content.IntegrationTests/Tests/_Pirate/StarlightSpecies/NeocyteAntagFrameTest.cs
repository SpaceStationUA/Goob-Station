using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Preferences.Managers;
using Content.Shared._Pirate.StarlightSpecies.Neocyte;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class NeocyteAntagFrameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: startingGear
          id: PirateNeocyteAntagFrameGear
          equipment:
            outerClothing2: ClothingOuterArmorNeocyteHeavySpaceUnremovable
        - type: loadoutGroup
          id: PirateNeocyteAntagFrames
          name: loadout-group-neocyte-cybernetics
          loadouts: [NeocyteArmorHeavySpace]
        - type: roleLoadout
          id: PirateNeocyteAntagFrameRole
          groups: [PirateNeocyteAntagFrames]
        """;

    [TestCase("Neocyte", "profile")]
    [TestCase("NeoKin", "profile")]
    [TestCase("Neocyte", "startingGear")]
    [TestCase("Neocyte", "roleLoadout")]
    public async Task AntagSpawnUsesSelectedFrameUnlessItsEquipmentOverridesIt(string species, string equipment)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var preferences = server.ResolveDependency<IServerPreferencesManager>();
        var session = pair.Player!;
        var loadout = new RoleLoadout("NeocyteLoadout");
        loadout.SelectedLoadouts["NeocyteCybernetics"] = [new Loadout { Prototype = "NeocyteArmorLightSpeed" }];
        await server.WaitPost(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies(species).WithLoadout(loadout);
            preferences.SetProfile(session.UserId, 0, profile).Wait();
        });
        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(session.AttachedEntity, Is.Null, "Exercise the direct antagonist spawn path.");
            var rule = em.SpawnEntity(null, MapCoordinates.Nullspace);
            var selection = em.AddComponent<AntagSelectionComponent>(rule);
            em.AddComponent<AntagLoadProfileRuleComponent>(rule);
            var definition = new AntagSelectionDefinition { PrefRoles = ["Nukeops"] };
            if (equipment == "startingGear")
                definition.StartingGear = "PirateNeocyteAntagFrameGear";
            if (equipment == "roleLoadout")
                definition.RoleLoadout = ["PirateNeocyteAntagFrameRole"];

            Assert.That(em.System<AntagSelectionSystem>().MakeAntag((rule, selection), session, definition), Is.True);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            mob = session.AttachedEntity!.Value;
            em.System<SharedTransformSystem>().SetMapCoordinates(mob, map.MapCoords);
            Assert.That(em.GetComponent<HumanoidAppearanceComponent>(mob).Species.Id, Is.EqualTo(species));
            if (equipment == "profile")
                Assert.That(em.GetComponent<NeocyteComponent>(mob).LastFramePrototype?.Id,
                    Is.EqualTo("ClothingOuterArmorNeocyteLightSpeedUnremovable"), "The saved choice must precede the random fallback.");
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.System<InventorySystem>().TryGetSlotEntity(mob, "outerClothing2", out var frame), Is.True);
            var expected = equipment == "profile"
                ? "ClothingOuterArmorNeocyteLightSpeedUnremovable"
                : "ClothingOuterArmorNeocyteHeavySpaceUnremovable";
            Assert.That(em.GetComponent<MetaDataComponent>(frame!.Value).EntityPrototype!.ID, Is.EqualTo(expected));
        });
        await pair.CleanReturnAsync();
    }
}
