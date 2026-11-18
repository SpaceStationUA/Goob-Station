// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using System.Linq;
using System.IO;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using SixLabors.ImageSharp.PixelFormats;
using ServerLibrary = Content.Server._Pirate.Crayon.CrayonLibrarySystem;
using ClientLibrary = Content.Client._Pirate.Crayon.CrayonLibrarySystem;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Globalization;
using Content.Client._Pirate.Crayon;
using Content.Client.Crayon.UI;
using Moq;
using Robust.Shared.Player;
using Content.Server.Examine;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Crayon;
using Content.IntegrationTests.Tests._Pirate.Newbies;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Charges.Systems;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Crayon;
using Content.Shared.Charges.Components;
using Content.Shared.Decals;
using Content.Shared.Input;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
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
            // Drawing mechanics are exercised by a player who has unlocked crayons.
            NoviceTestPlaytime.Set(Server.ResolveDependency<PlayTimeTrackingManager>(), ServerSession, 24);
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

    [TestCase(false, 1, CrayonMirror.None)]
    [TestCase(true, 1, CrayonMirror.None)]
    [TestCase(false, 4, CrayonMirror.Both)]
    [TestCase(true, 4, CrayonMirror.Both)]
    public async Task FastBrushAndEraserDragsLeaveContinuousStrokes(bool erase, int brushSize, CrayonMirror mirror)
    {
        CrayonCanvas canvas = null;
        await Client.WaitPost(() =>
        {
            canvas = new CrayonCanvas { Brush = erase ? null : Red, BrushSize = brushSize, Mirror = mirror };
            if (erase)
            {
                var filled = Symbol(CrayonSymbol.Small, Red);
                Array.Fill(filled.Pixels, (byte) 1);
                canvas.Load(filled);
            }

            Client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(canvas);
        });

        try
        {
            await RunTicks(1);
            await Key(BoundKeyState.Down, 2, 3);
            await Move(10, 3);
            await Move(14, 7);
            // The release may arrive at a new cell without a preceding mouse-move event.
            await Key(BoundKeyState.Up, 18, 7);
            await Move(25, 20);
            await Key(BoundKeyState.Down, 25, 20);
            await Key(BoundKeyState.Up, 25, 20);

            await Client.WaitAssertion(() =>
            {
                var expected = new HashSet<Vector2i>();
                for (var x = 2; x <= 10; x++)
                    AddBrush(x, 3);
                for (var d = 0; d <= 4; d++)
                    AddBrush(10 + d, 3 + d);
                for (var x = 14; x <= 18; x++)
                    AddBrush(x, 7);
                AddBrush(25, 20);

                var result = canvas.ToSymbol();
                for (var y = 0; y < result.Size; y++)
                for (var x = 0; x < result.Size; x++)
                {
                    var pixel = result.Pixels[y * result.Size + x];
                    Color? actual = pixel == 0 ? null : result.Palette[pixel - 1];
                    Color? color = expected.Contains(new Vector2i(x, y)) != erase ? Red : null;
                    Assert.That(actual, Is.EqualTo(color), $"Unexpected brush result at ({x}, {y})");
                }

                void AddBrush(int x, int y)
                {
                    var offset = (brushSize - 1) / 2;
                    for (var dy = 0; dy < brushSize; dy++)
                    for (var dx = 0; dx < brushSize; dx++)
                    {
                        var at = new Vector2i(x + dx - offset, y + dy - offset);
                        expected.Add(at);
                        if (mirror != CrayonMirror.Both)
                            continue;

                        var far = canvas.Size - 1;
                        expected.Add(new Vector2i(far - at.X, at.Y));
                        expected.Add(new Vector2i(at.X, far - at.Y));
                        expected.Add(new Vector2i(far - at.X, far - at.Y));
                    }
                }
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                canvas.Parent?.RemoveChild(canvas);
                canvas.Dispose();
            });
        }

        Vector2 Position(int x, int y) => new Vector2(x + 0.5f, y + 0.5f) * (canvas.PixelSize.X / (float) canvas.Size);

        Task Key(BoundKeyState state, int x, int y)
        {
            var pixel = Position(x, y);
            return Client.DoGuiEvent(canvas, new GUIBoundKeyEventArgs(
                EngineKeyFunctions.UIClick, state, new ScreenCoordinates(Vector2.Zero, default), false, pixel, pixel));
        }

        Task Move(int x, int y) => Client.WaitPost(() =>
        {
            var pixel = Position(x, y);
            // The integration helper dispatches key events; invoke the engine's mouse-move hook on its thread.
            typeof(CrayonCanvas).GetMethod("MouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(canvas, new object[]
                {
                    new GUIMouseMoveEventArgs(Vector2.Zero, canvas, pixel,
                        new ScreenCoordinates(Vector2.Zero, default), pixel, pixel),
                });
        });
    }

    [Test]
    public async Task RapidRotationClicksAccumulateBeforeServerConfirmation()
    {
        var crayon = await OpenWindow("CrayonArtist");
        var window = GetWindow<CrayonWindow>();
        var angleText = GetControlFromField<LineEdit>("_rotationValue", window);
        await Client.WaitAssertion(() =>
        {
            var requested = new List<float>();
            window.OnRotationSelected += requested.Add;
            var plusTen = FindButton(window, "+10")!;
            var minusOne = FindButton(window, "-1")!;
            Click(plusTen);
            Click(plusTen);
            Click(minusOne);
            Assert.That(requested, Is.EqualTo(new[] { 10f, 20f, 19f }));
            Assert.That(float.Parse(angleText.Text, CultureInfo.CurrentCulture), Is.EqualTo(19f));

            // Dispatch all clicks during one client callback, with no simulation or server ticks between them.
            static void Click(Button button)
            {
                button.Mode = BaseButton.ActionMode.Press;
                button.MuteSounds = true;
                foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
                {
                    typeof(BaseButton).GetMethod(state == BoundKeyState.Down ? "KeyBindDown" : "KeyBindUp",
                            BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(button, new object[]
                        {
                            new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state,
                                new ScreenCoordinates(Vector2.Zero, default), false, Vector2.Zero, Vector2.Zero),
                        });
                }
            }
        });
        await RunTicks(15);
        await Server.WaitAssertion(() =>
        {
            var angle = SEntMan.GetComponent<CrayonRotationComponent>(ToServer(crayon)).Rotation;
            Assert.That(angle.EqualsApprox(Angle.FromDegrees(19)), Is.True);
        });
        await Client.WaitAssertion(() =>
        {
            var angle = CEntMan.GetComponent<CrayonRotationComponent>(ToClient(crayon)).Rotation;
            Assert.That(angle.EqualsApprox(Angle.FromDegrees(19)), Is.True);
            Assert.That(float.Parse(angleText.Text, CultureInfo.CurrentCulture), Is.EqualTo(19f));
        });

        await PressKey(EngineKeyFunctions.EditorRotateObject);
        await RunTicks(15);
        await Client.WaitAssertion(() =>
            Assert.That(float.Parse(angleText.Text, CultureInfo.CurrentCulture), Is.EqualTo(-71f),
                "subsequent hotkey rotations still update the numeric controls"));
    }

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
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(await SaveDrawing(Symbol(CrayonSymbol.Large, Red))), crayon);
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
    public async Task LibraryLimitStaleEditorsAndPersistence()
    {
        await Server.WaitAssertion(() =>
        {
            var library = Server.System<ServerLibrary>();
            var owner = new NetUserId(Guid.NewGuid());
            var white = Symbol(CrayonSymbol.Small, White);
            var red = Symbol(CrayonSymbol.Large, Red);
            Assert.That(library.Save(owner, null, "first", white, out var first), Is.Null);
            for (var i = 1; i < 10; i++)
                Assert.That(library.Save(owner, null, i.ToString(), white, out _), Is.Null);
            Assert.That(library.Save(owner, null, "extra", white, out _), Is.EqualTo("pirate-crayon-library-full"));
            Assert.That(library.Save(owner, first, "first", red, out var revised), Is.Null);
            Assert.That(library.Delete(owner, first!.Value), Is.EqualTo("pirate-crayon-library-changed"));
            Assert.That(library.GetDrawings(owner)!.Single(d => d.Id == revised).Symbol.SameAs(red), Is.True);
            Assert.That(library.Delete(owner, revised!.Value), Is.Null);
            Assert.That(library.Save(owner, revised, "first", white, out _), Is.EqualTo("pirate-crayon-library-changed"));
            Assert.That(library.Save(owner, null, "new", red, out _), Is.Null);

            var store = new CrayonLibraryStore(Server.ResolveDependency<IResourceManager>().UserData);
            Assert.That(store.Read(owner), Has.Count.EqualTo(10));
            Assert.That(store.Read(new NetUserId(Guid.NewGuid())), Is.Empty);
        });
    }

    [Test]
    public async Task CustomStickerEditorAndLibraryUnlockAt24Hours()
    {
        var original = Symbol(CrayonSymbol.Small, White);
        var replacement = Symbol(CrayonSymbol.Large, Red);
        var name = Guid.NewGuid().ToString();
        var saved = await SaveDrawing(original, name);
        var tracking = Server.ResolveDependency<PlayTimeTrackingManager>();
        await Server.WaitPost(() => NoviceTestPlaytime.Set(tracking, ServerSession, 23.5));

        var crayon = await OpenWindow("CrayonArtist");
        var window = GetWindow<CrayonWindow>();
        await ClickControl(GetControlFromChildren<TextureButton>(button => button.ToolTip == name, window));
        var create = FindButton(window, CLoc("pirate-crayon-custom-new"))!;
        Assert.That(create.Disabled, Is.True);
        Assert.That(FindButton(window, CLoc("pirate-crayon-custom-edit"))!.Disabled, Is.True);
        Assert.That(FindButton(window, CLoc("pirate-crayon-custom-delete"))!.Disabled, Is.False);
        await ClickControl(create);
        Assert.That(TryFindWindow<CrayonCanvasWindow>(out _), Is.False);
        await AssertStencil(false);

        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(saved), crayon);
        await AssertStencil(false);
        var library = Client.System<ClientLibrary>();
        foreach (var previous in new Guid?[] { null, saved })
        {
            Task<CrayonSaveResult> saving = null!;
            await Client.WaitPost(() => saving = library.Save(previous, name + "-new", replacement));
            await RunTicks(15);
            Assert.That(saving.IsCompletedSuccessfully, Is.True);
            Assert.That((await saving).Error, Is.EqualTo("novice-protection-crayon-stickers"));
        }
        await Server.WaitAssertion(() =>
        {
            var drawings = Server.System<ServerLibrary>().GetDrawings(ServerSession.UserId)!;
            Assert.That(drawings.Single(d => d.Id == saved).Symbol.SameAs(original), Is.True);
            Assert.That(drawings.Any(d => d.Name == name + "-new"), Is.False);
        });

        await Server.WaitPost(() => NoviceTestPlaytime.Set(tracking, ServerSession, 24));
        await CloseBui(CrayonUiKey.Key, crayon);
        await UseInHand();
        await RunTicks(10);
        window = GetWindow<CrayonWindow>();
        create = FindButton(window, CLoc("pirate-crayon-custom-new"))!;
        Assert.That(create.Disabled, Is.False);
        await ClickControl(create);
        var editor = GetWindow<CrayonCanvasWindow>();
        await Client.WaitPost(() => editor.Close());

        var unlocked = await SaveDrawing(replacement);
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(unlocked), crayon);
        await AssertStencil(true);
    }

    [Test]
    public async Task SaveKeepsTheDraftWhenTheServerRejectsIt()
    {
        const string name = "duplicate draft";
        await SaveDrawing(Symbol(CrayonSymbol.Small, Red), name);
        var draft = Symbol(CrayonSymbol.Large, White);
        CrayonCanvasWindow editor = null;
        await Client.WaitAssertion(() =>
        {
            editor = new CrayonCanvasWindow(White, false);
            editor.Load(name, draft);
            editor.OpenCentered();
            typeof(CrayonCanvasWindow).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
            Assert.That(GetControlFromField<BoxContainer>("_body", editor).Visible, Is.False);
        });
        try
        {
            await RunTicks(15);
            await Client.WaitAssertion(() =>
            {
                Assert.That(editor.IsOpen, Is.True);
                Assert.That(GetControlFromField<BoxContainer>("_body", editor).Visible, Is.True);
                Assert.That(GetControlFromField<LineEdit>("_name", editor).Text, Is.EqualTo(name));
                Assert.That(GetControlFromField<CrayonCanvas>("_canvas", editor).ToSymbol().SameAs(draft), Is.True);
                Assert.That(GetControlFromField<Label>("_error", editor).Text, Is.EqualTo(CLoc("pirate-crayon-library-duplicate")));
            });
        }
        finally
        {
            await Client.WaitPost(() => editor.Close());
        }
    }

    [Test]
    public async Task ImportedImageFitsCanvasAndUsesBoxCrayons()
    {
        await Client.WaitAssertion(() =>
        {
            using var pixels = new SixLabors.ImageSharp.Image<Rgba32>(4, 2, new Rgba32(255, 0, 0, 255));
            pixels[0, 0] = new Rgba32(0, 0, 0, 0);
            using var file = new MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(pixels, file);
            file.Position = 0;
            var crayons = Client.System<CrayonPaletteSystem>().Crayons;
            var symbol = CrayonPngImport.Read(file, CrayonSymbol.Small, crayons);
            Assert.That(symbol?.IsValid(), Is.True);
            Assert.That(symbol!.Palette, Is.EqualTo(new[] { Red }));
            Assert.That(symbol.Pixels[0], Is.Zero);
            Assert.That(symbol.Pixels[8 * 32], Is.Zero);
            Assert.That(symbol.Pixels[16 * 32 + 16], Is.EqualTo(1));
            Assert.That(symbol.Pixels[24 * 32], Is.Zero);

            var oversized = file.ToArray();
            oversized[16] = 1;
            Assert.That(CrayonPngImport.Read(new MemoryStream(oversized), CrayonSymbol.Small, crayons), Is.Null);

            file.SetLength(2 * 1024 * 1024 + 1);
            file.Position = 0;
            Assert.That(CrayonPngImport.Read(file, CrayonSymbol.Small, crayons), Is.Null);
        });
    }

    [Test]
    public async Task ModerationDistinguishesSavedDrawingsFromFloorStamps()
    {
        var crayon = await OpenWindow("CrayonArtist");
        var saved = await SaveDrawing(Symbol(CrayonSymbol.Small, Red), "gallery");
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(saved), crayon);
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var library = Server.System<ServerLibrary>();
            var owner = ServerSession.UserId;
            var foreign = new NetUserId(Guid.NewGuid());
            var stamp = Stamps().Single().Owner;
            var netStamp = SEntMan.GetNetEntity(stamp);
            Assert.That(library.GetPage(owner, "", null, false, 0, null).Rows.Any(r => r.Saved == saved), Is.True);
            Assert.That(library.GetPage(owner, "", null, true, 0, null).Rows.Single().Stamp, Is.EqualTo(netStamp));

            var removeStamp = new CrayonAdminAction(null, netStamp, true);
            Assert.That(library.Moderate(ServerSession, foreign, null, removeStamp), Is.EqualTo("pirate-crayon-library-changed"));
            Assert.That(library.Moderate(ServerSession, owner, stamp, new CrayonAdminAction(null, netStamp, false)),
                Is.EqualTo("pirate-crayon-admin-copied"));
            Assert.That(library.GetDrawings(owner)!.Single(d => d.Name == "gallery (2)").Symbol.SameAs(Symbol(CrayonSymbol.Small, Red)), Is.True);

            Assert.That(library.Moderate(ServerSession, owner, null, new CrayonAdminAction(saved, null, true)),
                Is.EqualTo("pirate-crayon-admin-deleted"));
            Assert.That(SEntMan.IsQueuedForDeletion(stamp), Is.False);
            Assert.That(library.Moderate(ServerSession, owner, null, removeStamp), Is.EqualTo("pirate-crayon-admin-deleted"));
            Assert.That(SEntMan.IsQueuedForDeletion(stamp), Is.True);

            var copy = library.GetDrawings(owner)!.Single(d => d.Name == "gallery (2)");
            var nonAdmin = new Mock<ICommonSession>().Object;
            library.Moderate(nonAdmin, owner, null, new CrayonAdminAction(copy.Id, null, true));
            Assert.That(library.GetDrawings(owner)!.Any(d => d.Id == copy.Id), Is.True);
        });

        await RunTicks(15);
        await AssertStencil(false);
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(saved), crayon);
        await AssertStencil(false);
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
            Assert.That(stamp.Artist, Is.EqualTo(ServerSession.UserId));
            Assert.That(stamp.ArtistName, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName));
            var examine = Server.System<ExamineSystem>().GetExamineText(stamp.Owner, SPlayer);
            Assert.That(examine.ToString(), Does.Contain(stamp.ArtistName));
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
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(await SaveDrawing(Symbol(CrayonSymbol.Small, White))), crayon);
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
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(await SaveDrawing(Symbol(CrayonSymbol.Small, White))), crayon);
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
        var other = await SaveDrawing(symbol, "other copy");
        const string name = "cmu delete test";
        var saved = await SaveDrawing(symbol, name);
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(saved), crayon);
        await RunTicks(15);
        await AssertStencil(true);

        Button? delete = null;
        await Client.WaitPost(() => delete = FindButton(window, CLoc("pirate-crayon-custom-delete")));
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete!.Disabled, Is.False, "The drawn symbol is selected, so it can be deleted.");
        await ClickControl(delete);
        await RunTicks(15);

        await AssertStencil(false);
        await Server.WaitAssertion(() =>
        {
            var library = Server.System<ServerLibrary>().GetDrawings(ServerSession.UserId)!;
            Assert.That(library.Any(d => d.Id == other), Is.True, "An identical drawing in another slot must survive.");
            Assert.That(library.Any(d => d.Id == saved), Is.False);
        });
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
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(await SaveDrawing(Symbol(CrayonSymbol.Small, White))), first);

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
        await SendBui(CrayonUiKey.Key, new CrayonSelectSymbolMessage(await SaveDrawing(Symbol(CrayonSymbol.Small, White))), crayon);
        await AssertStencil(true);

        await PressKey(ContentKeyFunctions.SwapHands);
        await PlaceInHands("CrayonBox");
        await PressKey(EngineKeyFunctions.Use, 3, TargetCoords, NetEntity.Invalid);
        await RunTicks(30);
    }

    private async Task<Guid> SaveDrawing(CrayonSymbol symbol, string? name = null)
    {
        Task<CrayonSaveResult> saving = null!;
        await Client.WaitPost(() => saving = Client.System<ClientLibrary>().Save(null, name ?? Guid.NewGuid().ToString(), symbol));
        await RunTicks(15);
        Assert.That(saving.IsCompletedSuccessfully, Is.True);
        var result = await saving;
        Assert.That(result.Error, Is.Null);
        return result.Saved!.Value;
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
