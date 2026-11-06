// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.Paint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Crayon;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Pirate.Construction;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Content.Shared.RCD.Components;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

public sealed class NoviceBuildingAndDrawingTest : InteractionTest
{
    [TestCase(0, true)]
    [TestCase(23.99, true)]
    [TestCase(24, false)]
    [TestCase(25, false)]
    public async Task EveryGrilleRecipeUsesTotalPlaytime(double hours, bool restricted)
    {
        await SetPlaytime(hours);
        await Server.WaitAssertion(() =>
        {
            foreach (var recipe in new[] { "Grille", "GrilleDiagonal", "ClockGrille", "ClockworkGrilleDiagonal" })
            {
                var attempt = new ConstructAttemptEvent(recipe, LogError: false);
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref attempt);
                Assert.That(attempt.Cancelled, Is.EqualTo(restricted), recipe);
            }

            var ordinary = new ConstructAttemptEvent("Table", LogError: false);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref ordinary);
            Assert.That(ordinary.Cancelled, Is.False, "Ordinary construction remains available.");
        });
    }

    [TestCase(0, false)]
    [TestCase(24, true)]
    public async Task ManualConstructionDoesNotConsumeBlockedMaterials(double hours, bool allowed)
    {
        await SetPlaytime(hours);
        await StartConstruction("Grille");
        var rods = ToServer(await PlaceInHands("PartRodMetal", 2));
        await Interact();
        await AssertCount("Grille", allowed ? 1 : 0);
        if (!allowed)
            await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(rods).Count, Is.EqualTo(2)));
    }

    [TestCase(0, false)]
    [TestCase(24, true)]
    public async Task BrokenGrilleCannotBypassRestriction(double hours, bool allowed)
    {
        await SetPlaytime(hours);
        await SpawnTarget("GrilleBroken");
        var rods = ToServer(await PlaceInHands("PartRodMetal", 2));
        await Interact();
        await AssertCount("Grille", allowed ? 1 : 0);
        await AssertCount("GrilleBroken", allowed ? 0 : 1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(rods).Count, Is.EqualTo(allowed ? 1 : 2)));
    }

    [TestCase(0, false)]
    [TestCase(24, true)]
    public async Task RcdCannotBypassRestriction(double hours, bool allowed)
    {
        await SetPlaytime(hours);
        var rcd = ToServer(await PlaceInHands("RCD"));
        var charges = Server.System<SharedChargesSystem>();
        var before = 0;
        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<RCDComponent>(rcd).ProtoId = "Grille";
            before = charges.GetCurrentCharges(rcd);
        });
        await Interact(null, TargetCoords);
        await AssertCount("Grille", allowed ? 1 : 0);
        await Server.WaitAssertion(() => Assert.That(charges.GetCurrentCharges(rcd), Is.EqualTo(before - (allowed ? 4 : 0))));
    }

    [TestCase(0, false)]
    [TestCase(24, true)]
    public async Task StockAndCustomFloorDrawingRespectPlaytime(double hours, bool allowed)
    {
        await SetPlaytime(hours);
        var crayon = ToServer(await PlaceInHands("CrayonRed"));
        var charges = Server.System<SharedChargesSystem>();
        var decals = Server.System<SharedDecalSystem>();
        var before = 0;
        var decalCount = 0;
        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<CrayonComponent>(crayon).SelectedState = "0";
            before = charges.GetCurrentCharges(crayon);
            decalCount = decals.GetDecalsInRange(MapData.Grid.Owner, MapData.GridCoords.Position, 4).Count;
        });
        await Interact(null, TargetCoords);
        await Server.WaitAssertion(() =>
        {
            Assert.That(decals.GetDecalsInRange(MapData.Grid.Owner, MapData.GridCoords.Position, 4).Count,
                Is.EqualTo(decalCount + (allowed ? 1 : 0)));
            var symbol = new CrayonSymbol { Palette = [Color.Red], Pixels = new byte[CrayonSymbol.Small * CrayonSymbol.Small] };
            symbol.Pixels[0] = 1;
            Assert.That(Server.System<CrayonSymbolSystem>().TrySelect(crayon, SPlayer, symbol), Is.True);
        });
        await Interact(null, TargetCoords);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<CrayonStampComponent>().Count(), Is.EqualTo(allowed ? 1 : 0));
            Assert.That(charges.GetCurrentCharges(crayon), Is.EqualTo(before - (allowed ? 2 : 0)));
        });
    }

    [TestCase(0, false)]
    [TestCase(24, true)]
    public async Task CanvasStrokeCannotBypassRestriction(double hours, bool allowed)
    {
        await SetPlaytime(hours);
        await PlaceInHands("CrayonRed");
        var canvas = await SpawnTarget("PaintCanvas");
        await Interact();
        Assert.That(IsUiOpen(PaintUiKey.Key), Is.True);
        byte[] before = null!;
        await Server.WaitPost(() => before = (byte[]) SEntMan.GetComponent<PaintSurfaceComponent>(ToServer(canvas)).Image!.Pixels.Clone());
        await SendBui(PaintUiKey.Key, new PaintStrokeMessage(1, Color.Red, 1, PaintBrushStyle.Round,
            PaintShape.Freehand, false, 1, [new Vector2i(5, 5)]), canvas);
        await Server.WaitAssertion(() => Assert.That(
            SEntMan.GetComponent<PaintSurfaceComponent>(ToServer(canvas)).Image!.Pixels.SequenceEqual(before), Is.EqualTo(!allowed)));
    }

    [Test]
    public async Task UnloadedPlaytimeFailsClosedAndUnlocksWhenLoaded()
    {
        await SetPlaytime(24);
        var crayon = ToServer(await PlaceInHands("CrayonRed"));
        await Server.WaitAssertion(() =>
        {
            var tracking = Server.ResolveDependency<PlayTimeTrackingManager>();
            NoviceTestPlaytime.SetLoaded(tracking, ServerSession, false);
            try
            {
                var build = new ConstructAttemptEvent("Grille", LogError: false);
                SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref build);
                Assert.That(build.Cancelled, Is.True);
                var draw = new CrayonDrawImageEvent(SPlayer);
                SEntMan.EventBus.RaiseLocalEvent(crayon, ref draw);
                Assert.That(draw.Cancelled, Is.True);
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, ServerSession, true);
            }

            var unlocked = new CrayonDrawImageEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(crayon, ref unlocked);
            Assert.That(unlocked.Cancelled, Is.False);
        });
    }

    private Task AssertCount(string prototype, int expected) => Server.WaitAssertion(() =>
        Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == prototype), Is.EqualTo(expected)));

    private Task SetPlaytime(double hours) => Server.WaitPost(() =>
        NoviceTestPlaytime.Set(Server.ResolveDependency<PlayTimeTrackingManager>(), ServerSession, hours));
}
