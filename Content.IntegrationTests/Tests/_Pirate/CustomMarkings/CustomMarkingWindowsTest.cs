// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Pirate.CustomMarkings;
using Content.Client._Pirate.CustomMarkings.UI;
using Content.IntegrationTests.Pair;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>The library and editor windows open, lay out and fill in from the server without errors.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingLibraryWindow))]
[TestOf(typeof(CustomMarkingEditorWindow))]
public sealed class CustomMarkingWindowsTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task SaveCachesSubmittedArtTest(bool eraseEnabled)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        var system = client.System<CustomMarkingSystem>();
        var submitted = new CustomMarkingArt();
        submitted.SetPixel(0, CustomMarkingArt.South, 16, 12, new Rgba32(10, 200, 10, 255));
        submitted.SetErased(CustomMarkingArt.South, 1, 1, true);
        var accepted = submitted.Clone();
        if (!eraseEnabled)
            accepted.ClearErase(CustomMarkingArt.South);
        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CustomMarkingCVars.EraseBody, eraseEnabled));
        await pair.RunTicksSync(10);

        CustomMarkingEditorWindow editor = default!;
        CustomMarkingArt edited = default!;
        CustomMarkingEntry? saved = null;
        Button save = default!;
        await client.WaitPost(() =>
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
            editor = new CustomMarkingEditorWindow(null, submitted.Clone(), "Snapshot", null);
            editor.OnSaved += (_, entry) => saved = entry;
            editor.OpenCentered();
            save = Descendants(editor).OfType<Button>().Single(button => button.Text == Loc.GetString("wf-custom-marking-editor-save"));
            edited = Descendants(editor).OfType<CustomMarkingCanvas>().First().Art!;
            // Edit immediately after submission, before the network reply can run on the client thread.
            save.OnPressed += _ =>
            {
                edited.SetPixel(0, CustomMarkingArt.South, 16, 12, new Rgba32(200, 10, 10, 255));
                edited.AddFrame(0);
                edited.SetFrameTime(1, 750);
                edited.SetErased(CustomMarkingArt.South, 1, 1, true);
            };
        });

        await Click(pair, save);
        await pair.RunTicksSync(30);
        // Re-enabling erasure must not reveal a mask that the server discarded while saving.
        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CustomMarkingCVars.EraseBody, true));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            Assert.That(saved, Is.Not.Null);
            var entry = saved!.Value;
            Assert.That(edited.Same(submitted), Is.False, "edits occurred while the request was in flight");
            Assert.That(entry.Hash, Is.EqualTo(Content.Server._Pirate.CustomMarkings.CustomMarkingSystem.Hash(accepted)));
            Assert.That(system.TryReadArt(entry.Hash, out var cached), Is.True);
            Assert.That(cached!.Same(accepted), Is.True, "the cache matches the submitted snapshot as accepted by the server");
            Assert.That(editor.IsOpen, Is.False);
            localization.SetCulture(previousCulture!);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PickerGestureDoesNotDrawTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        var original = new CustomMarkingArt();
        original.SetPixel(0, CustomMarkingArt.South, 4, 5, new Rgba32(10, 200, 10, 255));
        CustomMarkingEditorWindow editor = default!;
        CustomMarkingCanvas canvas = default!;
        CustomMarkingIconButton picker = default!;
        await client.WaitPost(() =>
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
            editor = new CustomMarkingEditorWindow(null, original.Clone(), "Picker", null);
            editor.OpenCentered();
            editor.Measure(new Vector2(1920, 1080));
            editor.Arrange(UIBox2.FromDimensions(Vector2.Zero, editor.DesiredSize));
            canvas = Descendants(editor).OfType<CustomMarkingCanvas>().First();
            picker = Icon(editor, "wf-custom-marking-tool-picker");
        });
        await Click(pair, picker);

        var cell = canvas.PixelSize.X / (float) CustomMarkingRules.FrameSize;
        var picked = new Vector2(4.5f, 5.5f) * cell;
        var moved = new Vector2(8.5f, 5.5f) * cell;
        var screen = new ScreenCoordinates(Vector2.Zero, default);
        await client.DoGuiEvent(canvas, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Down, screen, false, picked, picked));
        await client.WaitAssertion(() =>
        {
            // The integration input helper exposes keys; dispatch the engine's mouse-move hook on its thread.
            typeof(CustomMarkingCanvas).GetMethod("MouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(canvas, new object[] { new GUIMouseMoveEventArgs(moved - picked, canvas, moved, screen, moved, moved) });
            Assert.That(canvas.Art!.Same(original), Is.True, "dragging the picker never paints a stroke");
            Assert.That(Icon(editor, "wf-custom-marking-editor-undo").Disabled, Is.True);
        });
        await client.DoGuiEvent(canvas, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Up, screen, false, moved, moved));
        await client.WaitAssertion(() =>
        {
            Assert.That(Icon(editor, "wf-custom-marking-tool-pencil").Pressed, Is.True);
            Assert.That(canvas.Art!.Same(original), Is.True);
        });

        await client.DoGuiEvent(canvas, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Down, screen, false, moved, moved));
        await client.DoGuiEvent(canvas, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Up, screen, false, moved, moved));
        await client.WaitAssertion(() => Assert.That(canvas.Art!.GetPixel(0, CustomMarkingArt.South, 8, 5),
            Is.EqualTo(original.GetPixel(0, CustomMarkingArt.South, 4, 5))));
        await Click(pair, Icon(editor, "wf-custom-marking-editor-undo"));
        await client.WaitAssertion(() =>
        {
            Assert.That(canvas.Art!.Same(original), Is.True, "the following pencil stroke owns its undo step");
            editor.Close();
            localization.SetCulture(previousCulture!);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingEditorRemovesColorPopupTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        var ui = client.ResolveDependency<IUserInterfaceManager>();
        CustomMarkingEditorWindow editor = default!;
        Button custom = default!;
        await client.WaitPost(() =>
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
            editor = new CustomMarkingEditorWindow(null, new CustomMarkingArt(), "Popup", null);
            editor.OpenCentered();
            var picker = Descendants(editor).OfType<CustomMarkingColorPicker>().Single();
            custom = Descendants(picker).OfType<Button>().Single();
        });

        await Click(pair, custom);
        await client.WaitAssertion(() =>
        {
            var popup = ui.ModalRoot.Children.OfType<CustomMarkingColorPopup>().Single();
            Assert.That(popup.IsInsideTree, Is.True);
            editor.Close();
            Assert.Multiple(() =>
            {
                Assert.That(popup.Parent, Is.Null);
                Assert.That(popup.IsInsideTree, Is.False);
                Assert.That(popup.OnColorChanged, Is.Null, "the popup releases the callback that retains its editor");
            });
            localization.SetCulture(previousCulture!);
        });
        await pair.CleanReturnAsync();
    }

    private static async Task Click(TestPair pair, BaseButton button)
    {
        await pair.Client.WaitPost(() =>
        {
            button.Mode = BaseButton.ActionMode.Press;
            button.MuteSounds = true;
        });
        var screen = new ScreenCoordinates(Vector2.Zero, default);
        await pair.Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Down, screen, false, Vector2.Zero, Vector2.Zero));
        await pair.Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick, BoundKeyState.Up, screen, false, Vector2.Zero, Vector2.Zero));
    }

    [Test]
    public async Task WindowsOpenTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        // Pirate: the editor's downstream locale lives in uk-UA.
        var localization = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        await client.WaitPost(() => localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA")));
        var system = client.System<CustomMarkingSystem>();
        HumanoidCharacterProfile profile = default!;

        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 16, 12, new Rgba32(10, 200, 10, 255));

        CustomMarkingLibraryWindow library = null;
        CustomMarkingEditorWindow editor = null;
        await client.WaitPost(() =>
        {
            profile = HumanoidCharacterProfile.DefaultWithSpecies("Human"); // Pirate: requires the client's IoC context.
            library = new CustomMarkingLibraryWindow(() => profile);
            library.OpenCentered();
            editor = new CustomMarkingEditorWindow(null, art, "Leaf", profile);
            editor.OpenCentered();
            system.Save(0, "Leaf", CustomMarkingPlacement.Hair, art);
        });

        await pair.RunTicksSync(30);

        await client.WaitAssertion(() =>
        {
            Assert.That(system.Library, Has.Count.EqualTo(1), "the library reached the client");
            Assert.That(library.IsOpen, Is.True);
            Assert.That(editor.IsOpen, Is.True);

            var labels = Descendants(library).OfType<Label>().Select(label => label.Text).ToList();
            Assert.That(labels, Does.Contain("Leaf"), "the library lists the saved marking");
            Assert.That(labels, Does.Contain(Loc.GetString("wf-custom-marking-placement-hair")));
            Assert.That(Descendants(editor).OfType<CustomMarkingCanvas>().Count(), Is.EqualTo(1 + CustomMarkingRules.Facings),
                "the editor shows the drawing and each facing");

            // A window opens at its own size, whatever the screen's: the text above the library's list wraps
            // instead of stretching the window, and every button fits in what the window asks for.
            var viewport = new Vector2(1920, 1080);
            foreach (var window in new Control[] { library, editor })
            {
                window.Measure(viewport);
                var size = window.DesiredSize;
                Assert.That(size.X, Is.LessThan(900), $"{window.GetType().Name} is no wider than what it shows needs");
                Assert.That(size.Y, Is.LessThan(768), $"{window.GetType().Name} fits a small screen");

                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                foreach (var button in Descendants(window).OfType<BaseButton>())
                {
                    var what = button is Button text ? text.Text : button.ToolTip;
                    var bottomRight = button.GlobalPosition - window.GlobalPosition + button.Size;
                    Assert.That(bottomRight.X, Is.LessThanOrEqualTo(size.X), $"{what} fits horizontally");
                    Assert.That(bottomRight.Y, Is.LessThanOrEqualTo(size.Y), $"{what} fits vertically");
                }
            }

            // Tools and actions are icons, so each must say what it does.
            var icons = Descendants(editor).OfType<CustomMarkingIconButton>().ToList();
            Assert.That(icons, Has.Count.GreaterThanOrEqualTo(13), "the tools, undo and redo and the facing operations are icon buttons");
            Assert.That(icons.All(icon => !string.IsNullOrEmpty(icon.ToolTip)), Is.True, "every icon button has a tooltip");
            Assert.That(icons.Single(icon => icon.ToolTip == Loc.GetString("wf-custom-marking-editor-symmetry")).ToggleMode,
                Is.True, "mirror drawing is switched on and off");

            // The body eraser is a tool of its own, and an animated marking's frames have their controls.
            foreach (var loc in FrameControls.Append("wf-custom-marking-tool-bodyeraser"))
            {
                Assert.That(icons.Count(icon => icon.ToolTip == Loc.GetString(loc)), Is.EqualTo(1), loc);
            }

            // A still marking has one frame: nothing to step through, play or take out, and no time to set.
            Assert.Multiple(() =>
            {
                Assert.That(Icon(editor, "wf-custom-marking-editor-frame-add").Disabled, Is.False);
                Assert.That(Icon(editor, "wf-custom-marking-editor-frame-remove").Disabled, Is.True);
                Assert.That(Icon(editor, "wf-custom-marking-editor-frame-previous").Disabled, Is.True);
                Assert.That(Icon(editor, "wf-custom-marking-editor-frame-play").Disabled, Is.True);
                Assert.That(Descendants(editor).OfType<FloatSpinBox>().Single().Parent!.Visible, Is.False);
            });

            // The body's hair can be left out of the picture, to see what is under it; it is there to start with.
            Assert.That(Descendants(editor).OfType<CheckBox>().Count(box => box.Text == Loc.GetString("wf-custom-marking-editor-show-hair")),
                Is.EqualTo(1));
            Assert.That(Descendants(editor).OfType<CustomMarkingCanvas>().Select(canvas => canvas.HideHair), Has.All.False);

            // The library shows each marking from its finished sprite, so an animated one plays there.
            Assert.That(Descendants(library).OfType<CustomMarkingCanvas>().Select(canvas => canvas.ArtState), Has.All.Not.Null);

            // A facing tile is picked by clicking anywhere on it, so its preview must not take the click itself.
            var canvases = Descendants(editor).OfType<CustomMarkingCanvas>().ToList();
            Assert.That(canvases.Count(canvas => canvas.MouseFilter == Control.MouseFilterMode.Ignore), Is.EqualTo(CustomMarkingRules.Facings));
            Assert.That(canvases.Count(canvas => canvas.MouseFilter == Control.MouseFilterMode.Stop), Is.EqualTo(1), "only the canvas is drawn on");
            Assert.That(Descendants(library).OfType<CustomMarkingIconButton>().All(icon => !string.IsNullOrEmpty(icon.ToolTip)), Is.True);
            Assert.That(Descendants(library).OfType<CustomMarkingIconButton>().Count(), Is.EqualTo(3), "a library row offers edit, export and delete");

            // Wearing from outside, as the creator does when a profile loads.
            library.SetWorn(new List<CustomMarking> { new(system.Library[0].Hash, CustomMarkingPlacement.Hair) });
            Assert.That(Descendants(library).OfType<Button>().Any(button => button.Text == Loc.GetString("wf-custom-marking-library-take-off")),
                Is.True, "a worn marking offers to come off");

            editor.Close();
            library.Close();
        });

        await pair.RunTicksSync(5);

        // An animated marking that erases a little of the body, and one that erases all of it and draws nothing.
        var blinking = art.Clone();
        blinking.AddFrame(0);
        blinking.SetFrameTime(1, 750);
        blinking.SetErased(CustomMarkingArt.South, 16, 16, true);
        var vanishing = new CustomMarkingArt();
        for (var y = 0; y < CustomMarkingRules.FrameSize; y++)
        {
            for (var x = 0; x < CustomMarkingRules.FrameSize; x++)
            {
                vanishing.SetErased(CustomMarkingArt.South, x, y, true);
            }
        }

        await client.WaitAssertion(() =>
        {
            var animated = new CustomMarkingEditorWindow(null, blinking, "Blink", profile);
            animated.OpenCentered();
            var tooMuch = Loc.GetString("wf-custom-marking-editor-erase-too-much", ("percent", CustomMarkingErase.MinKeptPercent));
            Assert.Multiple(() =>
            {
                foreach (var loc in FrameControls)
                {
                    Assert.That(Icon(animated, loc).Disabled, Is.False, loc);
                }

                var time = Descendants(animated).OfType<FloatSpinBox>().Single();
                Assert.That(time.Parent!.Visible, Is.True, "each frame of an animated marking has its time");
                Assert.That(time.Value, Is.EqualTo(CustomMarkingRules.DefaultFrameTime / 1000f).Within(0.001f), "the first frame's");
                Assert.That(Descendants(animated).OfType<Label>().Select(label => label.Text),
                    Does.Contain(Loc.GetString("wf-custom-marking-editor-frame-count", ("frame", 1), ("frames", 2))));
                Assert.That(Descendants(animated).OfType<Label>().Select(label => label.Text), Does.Not.Contain(tooMuch));
                Assert.That(Descendants(animated).OfType<CustomMarkingCanvas>().Select(canvas => canvas.Erase), Has.All.Not.Null,
                    "the body is shown without what the marking erases");
            });

            var size = new Vector2(1920, 1080);
            animated.Measure(size);
            Assert.That(animated.DesiredSize.Y, Is.LessThan(768), "the editor still fits a small screen with the frame time showing");
            animated.Close();

            // Erasing more than a body may lose is said as it happens, not only found out in a round.
            var vanished = new CustomMarkingEditorWindow(null, vanishing, "Gone", profile);
            vanished.OpenCentered();
            Assert.That(Descendants(vanished).OfType<Label>().Select(label => label.Text), Does.Contain(tooMuch));
            vanished.Close();
        });

        await pair.RunTicksSync(5);
        await client.WaitPost(() => localization.SetCulture(previousCulture!));
        await pair.CleanReturnAsync();
    }

    private static readonly string[] FrameControls =
    {
        "wf-custom-marking-editor-frame-previous",
        "wf-custom-marking-editor-frame-next",
        "wf-custom-marking-editor-frame-add",
        "wf-custom-marking-editor-frame-remove",
        "wf-custom-marking-editor-frame-play",
    };

    private static CustomMarkingIconButton Icon(Control window, string loc)
    {
        return Descendants(window).OfType<CustomMarkingIconButton>().Single(icon => icon.ToolTip == Loc.GetString(loc));
    }

    /// <summary>The creator's tiles list the library, show what is worn and keep a tile for a worn marking it doesn't hold.</summary>
    [Test]
    public async Task QuickListTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        // Pirate: the editor's downstream locale lives in uk-UA.
        var localization = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        await client.WaitPost(() => localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA")));
        var system = client.System<CustomMarkingSystem>();

        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 16, 12, new Rgba32(10, 200, 10, 255));

        CustomMarkingQuickList list = null;
        await client.WaitPost(() =>
        {
            list = new CustomMarkingQuickList();
            client.ResolveDependency<IUserInterfaceManager>().RootControl.AddChild(list);
            system.Save(0, "Leaf", CustomMarkingPlacement.Hair, art);
        });

        await pair.RunTicksSync(30);

        await client.WaitAssertion(() =>
        {
            Assert.That(system.Library, Has.Count.EqualTo(1));
            var entry = system.Library[0];

            var tiles = Descendants(list).OfType<Content.Client._Pirate.CustomMarkings.UI.CustomMarkingTile>().ToList();
            Assert.That(tiles, Has.Count.EqualTo(1), "a tile for the saved marking");
            Assert.That(tiles[0].Pressed, Is.False);
            Assert.That(tiles[0].ToolTip, Does.Contain("Leaf"));

            // Worn: its tile is pressed, and a worn marking the library doesn't hold gets a tile too.
            var stray = new CustomMarking(new string('d', CustomMarkingRules.HashLength), CustomMarkingPlacement.Skin);
            list.SetWorn(new List<CustomMarking> { new(entry.Hash, entry.Placement), stray });
            tiles = Descendants(list).OfType<Content.Client._Pirate.CustomMarkings.UI.CustomMarkingTile>().ToList();
            Assert.That(tiles, Has.Count.EqualTo(2));
            Assert.That(tiles.All(tile => tile.Pressed), Is.True);

            // With as many on as allowed, the rest can't be pressed.
            var max = client.CfgMan.GetCVar(CustomMarkingCVars.MaxWorn);
            var full = Enumerable.Range(0, max)
                .Select(i => new CustomMarking(i.ToString("x64"), CustomMarkingPlacement.Skin))
                .ToList();
            list.SetWorn(full);
            tiles = Descendants(list).OfType<Content.Client._Pirate.CustomMarkings.UI.CustomMarkingTile>().ToList();
            Assert.That(tiles, Has.Count.EqualTo(max + 1));
            Assert.That(tiles.Single(tile => !tile.Pressed).Disabled, Is.True);

            list.Orphan();
        });

        await pair.RunTicksSync(5);
        await client.WaitPost(() => localization.SetCulture(previousCulture!));
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
