// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Pirate.Server.Botany.Components;
using Content.Server._Pirate.Banking;
using Content.Server.Kitchen.Components;
using Content.Server.Power.Components;
using Content.Server.Silicons.Borgs;
using Content.Shared._Pirate.Banking;
using Content.Shared._Pirate.Banking.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Silicons;

public sealed class BorgServiceTest : InteractionTest
{
    protected override string PlayerPrototype => "PlayerBorgBattery";

    public override async Task Setup()
    {
        await base.Setup();
        await Server.WaitPost(() =>
        {
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(ServerSession.UserId);
            minds.TransferTo(mind.Owner, SPlayer);
        });
        await RunTicks(5);
    }

    private Task SelectChassis(string type) => SendBui(BorgSwitchableTypeUiKey.SelectBorgType,
        new BorgSelectTypeMessage(type, $"{type}_default"), Player);

    [TestCase("quadservice", "VendingMachineSeeds", "TomatoSeeds")]
    [TestCase("quadmedical", "VendingMachineMedical", "Gauze")]
    public async Task PurchasesUseTheOccupantsAccount(string type, string machine, string product)
    {
        await SelectChassis(type);
        await SpawnTarget(machine);
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(2);
        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), Is.True);

        var bank = Server.System<BankCardSystem>();
        var terminal = SEntMan.GetComponent<BankCardComponent>(SPlayer);
        Assert.That(terminal.AccountId, Is.Not.Null);
        Assert.That(bank.TryGetAccount(terminal.AccountId!.Value, out var account), Is.True);
        Assert.That(account!.Balance, Is.EqualTo(370));
        var vendor = SEntMan.GetComponent<VendingMachineComponent>(STarget!.Value);
        var entry = vendor.Inventory[product];
        var price = (int) (entry.Price * vendor.PriceMultiplier);
        var stock = entry.Amount;
        Assert.That(price, Is.GreaterThan(0));

        var buy = new VendingMachineEjectMessage(InventoryType.Regular, product);
        await SendBui(VendingMachineUiKey.Key, buy);
        Assert.That(entry.Amount, Is.EqualTo(stock - 1));
        Assert.That(account.Balance, Is.EqualTo(370 - price));
        Assert.That(account.Transactions.Single().Type, Is.EqualTo(BankTransactionType.Purchase));
        await RunSeconds(2f);
        await FindEntity(product, LookupFlags.Uncontained);

        await Server.WaitPost(() => bank.SetArrested(account.AccountId, true));
        await SendBui(VendingMachineUiKey.Key, buy);
        Assert.That(entry.Amount, Is.EqualTo(stock - 1));
        Assert.That(account.Balance, Is.EqualTo(370 - price));

        await Server.WaitAssertion(() =>
        {
            bank.SetArrested(account.AccountId, false);
            Assert.That(bank.TryChangeBalance(account.AccountId, -account.Balance, BankTransactionType.Adjustment), Is.True);
        });
        await SendBui(VendingMachineUiKey.Key, buy);
        Assert.That(entry.Amount, Is.EqualTo(stock - 1));
        Assert.That(account.Balance, Is.Zero);

        await Server.WaitAssertion(() =>
        {
            bank.PaySalary();
            Assert.That(account.Balance, Is.EqualTo(180));
            Assert.That(account.Transactions.Last().Type, Is.EqualTo(BankTransactionType.Salary));

            var minds = Server.System<SharedMindSystem>();
            var mind = minds.GetMind(SPlayer)!.Value;
            var replacement = SEntMan.SpawnEntity("PlayerBorgBattery", MapData.GridCoords);
            var replacementTerminal = SEntMan.GetComponent<BankCardComponent>(replacement);
            Assert.That(replacementTerminal.AccountId, Is.Null);
            minds.TransferTo(mind, replacement);
            Assert.That(terminal.AccountId, Is.Null);
            Assert.That(replacementTerminal.AccountId, Is.EqualTo(account.AccountId));
            Assert.That(account.Balance, Is.EqualTo(180));

            var nextOccupant = minds.CreateMind(null);
            minds.TransferTo(nextOccupant, SPlayer);
            Assert.That(terminal.AccountId, Is.Not.Null.And.Not.EqualTo(account.AccountId));
            Assert.That(bank.GetBalance(terminal.AccountId!.Value), Is.EqualTo(370));
        });
    }

    [Test]
    public async Task GardeningAndCookingHandsKeepTheSameContainers()
    {
        await SelectChassis("quadservice");
        EntityUid seeds = default;
        var borg = Server.System<BorgSystem>();
        var chassis = SEntMan.GetComponent<BorgChassisComponent>(SPlayer);
        string? Prototype(EntityUid id) => SEntMan.GetComponent<MetaDataComponent>(id).EntityPrototype?.ID;
        void Select(string module) => borg.SelectModule(SPlayer, chassis.ModuleContainer.ContainedEntities.Single(id => Prototype(id) == module));
        EntityUid Held(string item) => HandSys.EnumerateHeld(SPlayer).Single(id => Prototype(id) == item);

        await Server.WaitAssertion(() =>
        {
            Select("BorgModuleGardening");
            var analyzer = Held("PlantAnalyzer");
            seeds = SEntMan.SpawnEntity("TomatoSeeds", MapData.GridCoords);
            Assert.That(HandSys.IsHolding(SPlayer, analyzer, out var hand), Is.True);
            HandSys.TrySetActiveHand(SPlayer, hand);
            InteractSys.InteractUsing(SPlayer, analyzer, seeds, MapData.GridCoords);
        });
        await RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PlantAnalyzerComponent>(Held("PlantAnalyzer")).ScannedEntity, Is.EqualTo(seeds));
            var bucket = Held("Bucket");
            Assert.That(HandSys.IsHolding(SPlayer, bucket, out var bucketHand), Is.True);
            Assert.That(HandSys.TryDrop(SPlayer, bucket), Is.True);
            Select("BorgModuleCooking");
            Select("BorgModuleGardening");
            Assert.That(HandSys.EnumerateHeld(SPlayer).Any(id => Prototype(id) == "Bucket"), Is.False);
            Assert.That(HandSys.TryPickup(SPlayer, bucket, bucketHand!), Is.True);

            Select("BorgModuleCooking");
            var beaker = Held("LargeBeaker");
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var solution), Is.True);
            Assert.That(solutions.TryAddReagent(solution!.Value, "Water", 10), Is.True);
            var bowlHand = HandSys.EnumerateHands(SPlayer).Single(id => HandSys.HandIsEmpty(SPlayer, id));
            Assert.That(HandSys.IsHolding(SPlayer, beaker, out var beakerHand), Is.True);
            Assert.That(HandSys.TryDrop(SPlayer, beaker), Is.True);
            var bowl = SEntMan.SpawnEntity("FoodBowlBig", MapData.GridCoords);
            Assert.That(HandSys.TryPickup(SPlayer, bowl, bowlHand), Is.True);
            Assert.That(HandSys.TryPickup(SPlayer, beaker, beakerHand!), Is.True);
            Assert.That(solutions.GetTotalPrototypeQuantity(beaker, "Water").Float(), Is.EqualTo(10f));
            Assert.That(HandSys.TryDrop(SPlayer, bowl), Is.True);
            var soup = SEntMan.SpawnEntity("FoodSoupPea", MapData.GridCoords);
            Assert.That(HandSys.TryPickup(SPlayer, soup, bowlHand), Is.True);
        });
    }

    [Test]
    public async Task DragIngredientFromTheCounterIntoTheMicrowave()
    {
        await SelectChassis("quadservice");
        var microwave = await SpawnTarget("KitchenMicrowave");
        var counter = SEntMan.GetNetCoordinates(MapData.GridCoords.Offset(new Vector2(0.5f, 1.5f)));
        await SetTile(Plating, counter, MapData.Grid);
        await Spawn("Table", counter);
        var dough = await Spawn("FoodDough", counter);
        var oven = SEntMan.GetComponent<MicrowaveComponent>(ToServer(microwave));
        var power = Comp<ApcPowerReceiverComponent>(microwave);
        Assert.That(power.Powered, Is.False);
        await DragDrop(dough, microwave);
        Assert.That(oven.Storage.Count, Is.Zero, "An unpowered microwave cannot accept an ingredient.");
        await Spawn("APCBasic", PlayerCoords);
        await RunSeconds(1f);
        Assert.That(power.Powered, Is.True);
        await DragDrop(dough, microwave);
        Assert.That(oven.Storage.ContainedEntities, Does.Contain(ToServer(dough)));
        Assert.That(oven.LastUser, Is.EqualTo(SPlayer));

        await Server.WaitAssertion(() =>
        {
            var cook = SEntMan.SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(counter));
            var ingredient = SEntMan.SpawnEntity("FoodDough", SEntMan.GetCoordinates(counter));
            Assert.That(HandSys.TryPickupAnyHand(cook, ingredient), Is.True);
            InteractSys.InteractUsing(SPlayer, ingredient, ToServer(microwave), SEntMan.GetCoordinates(TargetCoords));
            Assert.That(HandSys.IsHolding(cook, ingredient), Is.True);
        });

        var second = await Spawn("FoodDough", PlayerCoords);
        await Server.WaitPost(() => oven.Capacity = 1);
        await DragDrop(second, microwave);
        Assert.That(oven.Storage.ContainedEntities, Is.EquivalentTo(new[] { ToServer(dough) }));
    }
}
