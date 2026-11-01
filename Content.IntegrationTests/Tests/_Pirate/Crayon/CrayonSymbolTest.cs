// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Globalization;
using Content.Client._Pirate.Crayon;
using Content.Client.Crayon.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Crayon;
using Content.Shared.Charges.Systems;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Crayon;
using Content.Shared.Charges.Components;
using Content.Shared.Decals;
using Content.Shared.Input;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Crayon;

[TestFixture]
public sealed class CrayonSymbolTest : InteractionTest
{
    protected override string PlayerPrototype => "PirateCrayonTestMob";

    // Pirate: the target InteractionTestMob has one hand; switching crayons needs two.
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PirateCrayonTestMob
  parent: InteractionTestMob
  components:
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands:
    - hand_right
    - hand_left
";

    private CultureInfo? _serverCulture;
    private CultureInfo? _clientCulture;

    public override async Task Setup()
    {
        await base.Setup();
        await Server.WaitPost(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            _serverCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
        });
        await Client.WaitPost(() =>
        {
            var loc = Client.ResolveDependency<ILocalizationManager>();
            _clientCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
        });
    }

    [TearDown]
    public async Task RestoreCulture()
    {
        if (_serverCulture != null)
            await Server.WaitPost(() => Server.ResolveDependency<ILocalizationManager>().SetCulture(_serverCulture));
        if (_clientCulture != null)
            await Client.WaitPost(() => Client.ResolveDependency<ILocalizationManager>().SetCulture(_clientCulture));
    }

    private static readonly Color White = Color.White;
    private static readonly Color Red = Color.Red;

    [Test]
    public async Task InvalidSymbolIsRejected()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Server.WaitAssertion(() =>
        {
            var symbols = Server.System<CrayonSymbolSystem>();
            var bad = new CrayonSymbol { Size = 33, Palette = { White }, Pixels = new byte[33 * 33] };
            Assert.That(symbols.TrySelect(crayon, SPlayer, bad), Is.False);
            Assert.That(Server.System<CrayonStencilSystem>().TryGetStencil(SPlayer, out _), Is.False);
        });
    }

    [Test]
    public async Task MalformedPixelsAndColorsDoNotReplaceTheStencil()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var original = Symbol(CrayonSymbol.Small, White);
        await Select(crayon, original);
        await Server.WaitAssertion(() =>
        {
            var symbols = Server.System<CrayonSymbolSystem>();
            var badIndex = Symbol(CrayonSymbol.Small, White);
            badIndex.Pixels[0] = 2;
            var nonFinite = Symbol(CrayonSymbol.Small, new Color(float.NaN, 0f, 0f));
            var transparent = Symbol(CrayonSymbol.Small, White.WithAlpha(0.5f));
            var missing = Symbol(CrayonSymbol.Small, White);
            missing.Pixels = null!;
            foreach (var bad in new[] { badIndex, nonFinite, transparent, missing })
                Assert.That(symbols.TrySelect(crayon, SPlayer, bad), Is.False);

            Assert.That(Server.System<CrayonStencilSystem>().TryGetStencil(SPlayer, out var stencil), Is.True);
            Assert.That(stencil, Is.SameAs(original));
        });
    }

    [Test]
    public async Task UnlimitedCrayonStampsMulticolorWithoutCreatingCharges()
    {
        var crayon = ToServer(await PlaceInHands("CrayonArtist"));
        await Select(crayon, Symbol(CrayonSymbol.Small, White, Red, Color.Blue));

        for (var i = 0; i < 35; i++)
            await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(35));
            Assert.That(SEntMan.HasComponent<LimitedChargesComponent>(crayon), Is.False);
            Assert.That(SEntMan.HasComponent<AutoRechargeComponent>(crayon), Is.False);
        });
    }

    [Test]
    public async Task PreciseRotationAndHotkeyApplyToStockDecalsAndCustomStamps()
    {
        var crayon = await OpenWindow("CrayonArtist");
        await SendBui(CrayonUiKey.Key, new CrayonRotationMessage(37.5f), crayon);
        var coordinates = MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f));
        await Interact(null, coordinates);

        await Server.WaitAssertion(() =>
        {
            var decals = Server.System<SharedDecalSystem>().GetDecalsInRange(MapData.Grid, coordinates.Position, 2f);
            Assert.That(decals.Any(d => Math.Abs(d.Decal.Angle.Degrees - 37.5) < 0.001), Is.True);
        });

        await PressKey(EngineKeyFunctions.EditorRotateObject);
        await RunTicks(15);
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(Symbol(CrayonSymbol.Large, Red)), crayon);
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var rotation = SEntMan.GetComponent<CrayonRotationComponent>(ToServer(crayon)).Rotation;
            var stampRotation = SEntMan.GetComponent<TransformComponent>(Stamps().Single().Owner).LocalRotation;
            Assert.That(rotation.EqualsApprox(Angle.FromDegrees(-52.5)), Is.True);
            Assert.That(stampRotation.EqualsApprox(rotation), Is.True);
        });
        await Client.WaitAssertion(() =>
        {
            var rotation = CEntMan.GetComponent<CrayonRotationComponent>(ToClient(crayon)).Rotation;
            Assert.That(rotation.EqualsApprox(Angle.FromDegrees(-52.5)), Is.True);
        });
    }

    [Test]
    public async Task InvalidRotationMessagesKeepThePreviousAngle()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CrayonRotationMessage(45f), crayon);
        foreach (var angle in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            await SendBui(CrayonUiKey.Key, new CrayonRotationMessage(angle), crayon);

        await Server.WaitAssertion(() =>
        {
            var rotation = SEntMan.GetComponent<CrayonRotationComponent>(ToServer(crayon)).Rotation;
            Assert.That(rotation.EqualsApprox(Angle.FromDegrees(45)), Is.True);
        });
    }

    [Test]
    public async Task EditorsFromDifferentCrayonsPreserveTheSavedLibrary()
    {
        await Client.WaitAssertion(() =>
        {
            var first = new CrayonSymbolStorage();
            var second = new CrayonSymbolStorage();
            var white = Symbol(CrayonSymbol.Small, White);
            var red = Symbol(CrayonSymbol.Large, Red);
            first.Save("first", white);
            var name = second.Save("second|drawing", red);
            Assert.That(name, Is.EqualTo("second drawing"));
            Assert.That(second.Symbols.Select(entry => entry.Name), Is.EquivalentTo(new[] { "first", name }));

            first.Delete("first");
            var reloaded = new CrayonSymbolStorage();
            Assert.That(reloaded.Symbols.Select(entry => entry.Name), Is.EqualTo(new[] { name }));
            Assert.That(reloaded.Symbols.Single().Symbol.SameAs(red), Is.True);

            second.Save("third", white);
            reloaded = new CrayonSymbolStorage();
            Assert.That(reloaded.Symbols.Select(entry => entry.Name), Is.EquivalentTo(new[] { name, "third" }),
                "saving from a stale editor must not resurrect a deleted drawing");
        });
    }

    [Test]
    public async Task StampsSingleColourSymbolOnTheFloor()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Small, White));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var stamp = Stamps().Single();
            Assert.That(stamp.Symbol.Size, Is.EqualTo(CrayonSymbol.Small));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
        });
    }

    [Test]
    public async Task MissingColourBlocksTheStamp()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Small, White, Red));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps(), Is.Empty);
            Assert.That(Charges(crayon), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task NearbyCrayonSuppliesTheOtherColour()
    {
        var red = ToServer(await Spawn("CrayonRed", PlayerCoords));
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Small, White, Red));
        var redBefore = 0;
        await Server.WaitPost(() => redBefore = Charges(red));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
            Assert.That(Charges(red), Is.EqualTo(redBefore - 1));
        });
    }

    [Test]
    public async Task TheColoursAreTheDefaultBoxCrayons()
    {
        await Server.WaitAssertion(() =>
        {
            var crayons = Server.System<CrayonPaletteSystem>().Crayons;
            Assert.That(crayons, Is.Not.Empty);
            Assert.That(crayons.Select(c => c.Color.ToHex()).Distinct().Count(), Is.EqualTo(crayons.Count),
                "One crayon per colour, however many the box holds of it.");
            Assert.That(crayons.Select(c => c.Name), Does.Not.Contain("crayon"), "Each colour has its crayon's name.");

            var factory = Server.ResolveDependency<IComponentFactory>();
            foreach (var crayon in crayons)
            {
                Assert.That(ProtoMan.Index<EntityPrototype>(crayon.Id).TryGetComponent(out CrayonComponent? comp, factory), Is.True, crayon.Name);
                Assert.That(comp!.SelectableColor, Is.False, $"{crayon.Name}: the rainbow crayon draws any colour, it isn't one.");
            }

            foreach (var crayon in crayons)
            {
                var shades = CrayonPaletteSystem.Shades(crayon.Color);
                Assert.That(shades.Select(s => s.Color.ToHex()).Distinct().Count(), Is.EqualTo(shades.Count), crayon.Name);
                Assert.That(shades.Any(s => s.Tone == 0), Is.True, crayon.Name);
            }

            Assert.That(CrayonPaletteSystem.Shades(Red).Count, Is.EqualTo(2 * CrayonPaletteSystem.Tones + 1),
                "Red runs from light to dark.");
        });
    }

    [Test]
    public async Task AGradientOfOneCrayonCostsOneCharge()
    {
        var crayon = ToServer(await PlaceInHands("CrayonRed"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Small,
            CrayonPaletteSystem.Shade(Red, 4), CrayonPaletteSystem.Shade(Red, 2), Red,
            CrayonPaletteSystem.Shade(Red, -2), CrayonPaletteSystem.Shade(Red, -4)));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
        });
    }

    [Test]
    public async Task MimeCrayonDrawsTheBoxWhite()
    {
        var crayon = ToServer(await PlaceInHands("CrayonMime"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Small, White, CrayonPaletteSystem.Shade(White, -3)));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1), "The mime crayon draws the box white's tones.");
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
        });
    }

    [Test]
    public async Task RainbowCrayonDrawsAllColoursAlone()
    {
        var crayon = ToServer(await PlaceInHands("CrayonRainbow"));
        var before = await Select(crayon, Symbol(CrayonSymbol.Large, White, Red, Color.Blue));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Single().Symbol.Size, Is.EqualTo(CrayonSymbol.Large));
            Assert.That(Charges(crayon), Is.EqualTo(before - 3), "One charge per colour.");
        });
    }

    [Test]
    public async Task EveryCrayonStampsTheStencil()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var proto in ProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.HasComp<CrayonComponent>(Factory))
                    continue;

                Assert.That(proto.HasComp<CrayonSymbolComponent>(Factory), Is.True,
                    $"{proto.ID} would draw its stock decal while a custom symbol is picked.");
                Assert.That(proto.HasComp<CrayonRotationComponent>(Factory), Is.True,
                    $"{proto.ID} must support both rotation controls.");
            }
        });
    }

    [Test]
    public async Task PickingAStockDecalClearsTheStencil()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(Symbol(CrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

        var before = 0;
        await Server.WaitPost(() => before = Charges(ToServer(crayon)));
        await SendBui(CrayonUiKey.Key, new CrayonSelectMessage("0"), crayon);
        await AssertStencil(false);

        // Decals go on a grid, so click with grid coordinates.
        await Interact(null, MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f)));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CrayonComponent>(ToServer(crayon)).SelectedState, Is.EqualTo("0"));
            Assert.That(Stamps(), Is.Empty);
            Assert.That(Charges(ToServer(crayon)), Is.EqualTo(before - 1), "The stock decal is drawn instead.");
        });
    }

    [Test]
    public async Task TypingADecalInTheSearchClearsTheStencil()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(Symbol(CrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

        var window = GetWindow<CrayonWindow>();
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Not.Null, "The window shows the symbol the server holds.");
            Assert.That(StatusState(crayon), Is.EqualTo(CLoc("pirate-crayon-status-custom")), "The crayon says it stamps the symbol.");
        });

        // Every digit is a stock decal, so the search picks one as soon as it is typed.
        var search = GetControlFromField<LineEdit>("Search", window);
        await Client.WaitPost(() => search.SetText("0", invokeEvent: true));
        await RunTicks(15);

        await AssertStencil(false);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CrayonComponent>(ToServer(crayon)).SelectedState, Is.EqualTo("0")));
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Null, "The window no longer shows the symbol.");
            Assert.That(StatusState(crayon), Is.EqualTo("0"), "The crayon says it draws the searched decal.");
        });
    }

    [Test]
    public async Task DeletingTheDrawnSymbolGoesBackToTheStockDecal()
    {
        var crayon = await OpenWindow("CrayonWhite");
        var window = GetWindow<CrayonWindow>();
        var symbol = Symbol(CrayonSymbol.Small, White);
        const string name = "cmu delete test";
        await Client.WaitPost(() =>
        {
            var storage = (CrayonSymbolStorage) typeof(CrayonWindow)
                .GetField("_pirateStorage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            storage.Save(name, symbol);
            typeof(CrayonWindow).GetMethod("PirateSelect", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, new object[] { name, symbol });
        });
        await RunTicks(15);
        await AssertStencil(true);

        Button? delete = null;
        await Client.WaitPost(() => delete = FindButton(window, CLoc("pirate-crayon-custom-delete")));
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete!.Disabled, Is.False, "The drawn symbol is selected, so it can be deleted.");
        await ClickControl(delete);
        await RunTicks(15);

        await AssertStencil(false);
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Null);
            Assert.That(StatusState(crayon), Is.Not.EqualTo(CLoc("pirate-crayon-status-custom")), "The crayon draws its stock decal again.");
        });
    }

    [Test]
    public async Task TheStencilFollowsThePlayerToAnotherCrayon()
    {
        var first = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(Symbol(CrayonSymbol.Small, White)), first);

        await PressKey(ContentKeyFunctions.SwapHands);
        var second = ToServer(await PlaceInHands("CrayonWhite"));
        int firstBefore = 0, secondBefore = 0;
        await Server.WaitPost(() =>
        {
            firstBefore = Charges(ToServer(first));
            secondBefore = Charges(second);
        });

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1), "A crayon never opened still stamps the picked symbol.");
            Assert.That(Charges(second), Is.EqualTo(secondBefore - 1), "The held crayon draws it.");
            Assert.That(Charges(ToServer(first)), Is.EqualTo(firstBefore));
        });
        await AssertStencil(true);
    }

    [Test]
    public async Task TheStencilOutlivesAUsedUpCrayon()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Select(crayon, Symbol(CrayonSymbol.Small, White));
        await Server.WaitPost(() => Server.System<SharedChargesSystem>().SetCharges(crayon, 1));

        await Interact(null, TargetCoords);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crayon), Is.True, "The last charge uses the crayon up.");
            Assert.That(Stamps().Count(), Is.EqualTo(1));
        });

        await PlaceInHands("CrayonWhite");
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() => Assert.That(Stamps().Count(), Is.EqualTo(2), "The next crayon stamps it too."));
        await AssertStencil(true);
    }

    [Test]
    public async Task DrawingElsewhereGetsTheStencilImage()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var symbol = Symbol(CrayonSymbol.Small, White);
        await Server.WaitAssertion(() =>
        {
            var plain = new CrayonDrawImageEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(crayon, ref plain);
            Assert.That(plain.Pixels, Is.Null, "Without a stencil the decal draws.");
        });

        var before = await Select(crayon, symbol);
        await Server.WaitAssertion(() =>
        {
            var image = new CrayonDrawImageEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(crayon, ref image);
            Assert.That(image.Cancelled, Is.False);
            Assert.That(image.Size, Is.EqualTo(symbol.Size));
            Assert.That(image.Pixels, Is.EqualTo(symbol.Pixels));
            Assert.That(image.Palette, Is.EqualTo(symbol.Palette));
            Assert.That(Server.System<SharedChargesSystem>().GetCurrentCharges(crayon), Is.EqualTo(before - 1), "It costs what a stamp costs.");
        });
    }

    [Test]
    public async Task SpaceCleanerWashesTheStampOff()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Select(crayon, Symbol(CrayonSymbol.Small, White));
        await Interact(null, TargetCoords);

        await Server.WaitPost(() =>
        {
            var tile = SEntMan.System<SharedMapSystem>()
                .GetTileRef(MapData.Grid, MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var cleaner = ProtoMan.Index<ReagentPrototype>("SpaceCleaner");
            new CleanCrayonStampsReaction().TileReact(tile, cleaner, FixedPoint2.New(10), SEntMan, null);
        });
        await RunTicks(2);

        await Server.WaitAssertion(() => Assert.That(Stamps(), Is.Empty));
    }

    [Test]
    public async Task SelectingFromTheWindowThenClickingWithAnotherItemKeepsPredictionIntact()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(Symbol(CrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

        await PressKey(ContentKeyFunctions.SwapHands);
        await PlaceInHands("CrayonBox");
        await PressKey(EngineKeyFunctions.Use, 3, TargetCoords, NetEntity.Invalid);
        await RunTicks(30);
    }

    private static CrayonSymbol Symbol(int size, params Color[] colors)
    {
        var symbol = new CrayonSymbol { Size = size, Pixels = new byte[size * size] };
        symbol.Palette.AddRange(colors);
        for (var i = 0; i < colors.Length; i++)
        {
            symbol.Pixels[i] = (byte) (i + 1);
        }

        return symbol;
    }

    private async Task<NetEntity> OpenWindow(string crayon)
    {
        var held = await PlaceInHands(crayon);
        await RunTicks(10);
        await UseInHand();
        await RunTicks(10);
        return held;
    }

    /// <summary>
    /// Whether the player has a custom symbol picked, on the server and as their client sees it.
    /// </summary>
    private async Task AssertStencil(bool set)
    {
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<CrayonStencilSystem>().TryGetStencil(SPlayer, out _), Is.EqualTo(set)));
        await Client.WaitAssertion(() =>
            Assert.That(Client.System<CrayonStencilSystem>().TryGetStencil(CPlayer, out _), Is.EqualTo(set)));
    }

    // The window and the held crayon's status line, as the player sees them.
    private static object? WindowStencil(CrayonWindow window)
    {
        return typeof(CrayonWindow).GetField("_pirateStencil", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
    }

    private string? StatusState(NetEntity crayon)
    {
        var uid = CEntMan.GetEntity(crayon);
        var state = typeof(Content.Client.Crayon.CrayonSystem).GetMethod("PirateState", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string?) state.Invoke(null, new object[] { new Entity<CrayonComponent>(uid, CEntMan.GetComponent<CrayonComponent>(uid)) });
    }

    private static Button? FindButton(Robust.Client.UserInterface.Control parent, string text)
    {
        foreach (var child in parent.Children)
        {
            if (child is Button button && button.Text == text)
                return button;

            if (FindButton(child, text) is { } found)
                return found;
        }

        return null;
    }

    private string CLoc(string id) => Client.ResolveDependency<ILocalizationManager>().GetString(id);

    private async Task<int> Select(EntityUid crayon, CrayonSymbol symbol)
    {
        var charges = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<CrayonSymbolSystem>().TrySelect(crayon, SPlayer, symbol), Is.True);
            charges = Charges(crayon);
        });
        return charges;
    }

    private int Charges(EntityUid crayon) => Server.System<SharedChargesSystem>().GetCurrentCharges(crayon);

    private IEnumerable<CrayonStampComponent> Stamps() => SEntMan.EntityQuery<CrayonStampComponent>();
}
