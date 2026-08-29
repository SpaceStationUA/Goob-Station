// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Construction.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Hands.Components;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class ConstructionCraftingIntegrationTest : InteractionTest
{
    private static readonly (string Start, string Result)[] RevolutionaryWeapons =
    [
        ("RevSwordBlade", "RevSword"),
        ("RevMaceHead", "RevMace"),
        ("RevKnifeBlade", "RevKnife"),
        ("RevPitchforkHead", "RevPitchfork"),
        ("RevTorchHead", "RevTorch"),
        ("RevMaulHead", "RevMaul"),
        ("RevKunaiHead", "RevKunai"),
    ];

    [Test]
    public async Task PlayerCanFoldAWorldPaperItemIntoOnePaperSheet()
    {
        // Paper is both an entity ID and a stack type ID, so PlaceInHands("Paper") would spawn SheetPaper1.
        var inputNet = await Spawn("Paper");
        await Pickup(inputNet);
        var input = ToServer(inputNet);
        var player = ToServer(Player);
        var recipe = ProtoMan.Index<ConstructionPrototype>("SheetPaper");
        var graph = ProtoMan.Index<ConstructionGraphPrototype>(recipe.Graph);
        var path = graph.Path(recipe.StartNode, recipe.TargetNode);
        var edge = graph.Nodes[recipe.StartNode].GetEdge(path![0].Name);
        var tagStep = edge!.Steps.OfType<TagConstructionGraphStep>().Single();
        var resultPrototype = ProtoMan.Index<EntityPrototype>("SheetPaper");
        var constructionName = Factory.GetComponentName<ConstructionComponent>();

        Assert.Multiple(() =>
        {
            Assert.That(recipe.Type, Is.EqualTo(ConstructionType.Item));
            Assert.That(path, Is.Not.Null.And.Length.EqualTo(1));
            Assert.That(SEntMan.HasComponent<HandsComponent>(player), Is.True);
            Assert.That(SEntMan.System<ActionBlockerSystem>().CanInteract(player, null), Is.True);
            Assert.That(tagStep.EntityValid(input, SEntMan, Factory), Is.True,
                "A normal world Paper entity must satisfy the recipe's Paper tag step.");
            Assert.That(resultPrototype.Components.ContainsKey(constructionName), Is.True,
                "The construction result must carry the graph node expected by item construction.");
        });

        Task<bool> crafting = default!;
        await Server.WaitPost(() => crafting = SConstruction.TryStartItemConstruction(recipe.ID, player));

        Task? tickTask = null;
        while (!crafting.IsCompleted)
        {
            tickTask = Pair.RunTicksSync(1);
            await Task.WhenAny(crafting, tickTask);
        }

        if (tickTask != null)
            await tickTask;

        Assert.That(await crafting, Is.True,
            $"Paper folding failed after all recipe preconditions passed; input deleted: {SEntMan.Deleted(input)}, " +
            $"active item: {HandSys.GetActiveItem((player, Hands))}.");
        await RunTicks(5);

        var result = await FindEntity(("SheetPaper", 1));
        var stack = SEntMan.GetComponent<StackComponent>(result);

        Assert.That(stack.StackTypeId.Id, Is.EqualTo("Paper"));
        Assert.That(stack.Count, Is.EqualTo(1),
            "Folding one world paper item must not create a full paper stack.");
    }

    [Test]
    public async Task EveryRevolutionaryCompletionGraphWorksThroughPlayerInteractions()
    {
        foreach (var (start, result) in RevolutionaryWeapons)
        {
            await CompleteTarget(start, result, "finished",
                ("MaterialWoodPlank1", 1));
        }

        await CompleteTarget("RevGasFilter", "ClothingMaskGasRev", "finished",
            ("SheetPlastic1", 4),
            ("Screwdriver", 1));

        await CompleteTarget("RevHelmetLiner", "ClothingHeadHelmetRev", "finished",
            ("SheetPlasteel1", 5),
            ("Welder", 1),
            ("MeleeHammer", 1),
            ("Screwdriver", 1),
            ("RevBolt", 1),
            ("RevNut", 1),
            ("Wrench", 1));

        await CompleteTarget("ClothingShoesBootsRev", "ClothingShoesBootsRevSteel", "finished",
            ("RevArmorPlate", 1),
            ("MeleeHammer", 1),
            ("Screwdriver", 1));

        await CompleteTarget("RevEmptyVest", "ClothingOuterVestWebRev", "bullet",
            ("RevArmorPlate", 1),
            ("MeleeHammer", 1),
            ("RevBolt", 2),
            ("RevNut", 2),
            ("Wrench", 1));

        await CompleteTarget("RevEmptyVest", "ClothingOuterVestReflectRev", "reflect",
            ("RevReflectivePlate", 1),
            ("Screwdriver", 1),
            ("CableApcStack1", 5),
            ("Wirecutter", 1),
            ("SheetPlastic1", 4),
            ("Welder", 1));

        await CompleteTarget("PowerArmorFrame", "T45PowerArmor", "T45",
            ("T45Helmet", 1),
            ("T45Chest", 1),
            ("T45ArmL", 1),
            ("T45ArmR", 1),
            ("T45LegL", 1),
            ("T45LegR", 1),
            ("Welder", 1),
            ("RevArmorPlate", 1),
            ("MeleeHammer", 1),
            ("Screwdriver", 1),
            ("Wrench", 1),
            ("RevArmorPlate", 1),
            ("MeleeHammer", 1),
            ("Screwdriver", 1),
            ("FusionCore", 1),
            ("RevArmorPlate", 1),
            ("RevArmorPlate", 1),
            ("RevArmorPlate", 1),
            ("Wrench", 1),
            ("Welder", 1),
            ("Wrench", 1),
            ("RevGunParts", 3),
            ("RevBolt", 1),
            ("RevNut", 1),
            ("Wrench", 1),
            ("Welder", 1));
    }

    private async Task CompleteTarget(
        string start,
        string result,
        string resultNode,
        params (string Prototype, int Quantity)[] steps)
    {
        await SpawnTarget(start);
        foreach (var (prototype, quantity) in steps)
            await InteractUsing(prototype, quantity);

        AssertPrototype(result);
        var target = ToServer(Target!.Value);
        var construction = SEntMan.GetComponent<ConstructionComponent>(target);
        Assert.That(construction.Node, Is.EqualTo(resultNode),
            $"'{start}' became '{result}' but stopped at construction node '{construction.Node}'.");

        await DeleteHeldEntity();
        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
        Target = null;
        await RunTicks(1);
    }
}
