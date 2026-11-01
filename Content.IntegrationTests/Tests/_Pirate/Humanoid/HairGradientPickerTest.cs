// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Goobstation.UIKit.UserInterface.Humanoid;
using Content.IntegrationTests.Pair;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

[TestFixture]
[TestOf(typeof(MarkingGridPicker))]
public sealed class HairGradientPickerTest
{
    [TestCase("HairGradientRGB2Col", 2)]
    [TestCase("HairGradientRGB3Col", 3)]
    [TestCase("HairGradientHSV", 2)]
    [TestCase("FacialHairGradientRGB2Col", 2)]
    [TestCase("FacialHairGradientRGB3Col", 3)]
    [TestCase("FacialHairGradientHSV", 2)]
    public async Task SavedGradientColorsAndParametersCanBeEdited(string id, int colorCount)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var culture = localization.DefaultCulture;
        MarkingGridPicker picker = null;
        ContainerButton applied = null;
        MarkingSet changed = null;
        var category = MarkingCategories.HairSpecial;
        var initialParameters = new Color(0.15f, 0.4f, colorCount == 3 ? 0.8f : 0f, 0f);

        try
        {
            await client.WaitAssertion(() =>
            {
                localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
                var prototype = client.ResolveDependency<IPrototypeManager>().Index<MarkingPrototype>(id);
                category = prototype.MarkingCategory;
                var marking = prototype.AsMarking();
                for (var i = 0; i < colorCount; i++)
                    marking.SetColor(i, Color.Red);
                marking.SetColor(colorCount, initialParameters);

                picker = CreatePicker(category);
                picker.SetData(new List<Marking> { marking }, "Human", Sex.Male, Color.White, Color.Black);
                picker.OnMarkingColorChange += set => changed = new MarkingSet(set);
                client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(picker);
                applied = picker.FindControl<BoxContainer>("AppliedList").Children.OfType<ContainerButton>().Single();
            });
            await Click(pair, applied);

            await client.WaitAssertion(() =>
            {
                var editor = picker.FindControl<BoxContainer>("ColorEditor");
                Assert.That(editor.Visible, Is.True, "shader-only markings must expose their editor");
                var colors = editor.Children.OfType<ColorSelectorSliders>().ToArray();
                var parameters = ParameterSliders(editor);
                Assert.That(colors, Has.Length.EqualTo(colorCount));
                Assert.That(parameters, Has.Length.EqualTo(colorCount));
                Assert.That(colors.Select(selector => selector.Color), Has.All.EqualTo(Color.Red));
                Assert.That(parameters[0].Value, Is.EqualTo(initialParameters.R));
                Assert.That(parameters[1].Value, Is.EqualTo(initialParameters.G));
                if (colorCount == 3)
                    Assert.That(parameters[2].Value, Is.EqualTo(initialParameters.B));

                colors[0].OnColorChanged!.Invoke(Color.Blue);
                parameters[0].Value = 0.25f;
            });
            await pair.RunTicksSync(30);

            await client.WaitAssertion(() =>
            {
                Assert.That(changed, Is.Not.Null, "the profile receives the edited marking");
                var colors = changed.Markings[category].Single().MarkingColors;
                Assert.That(colors[0], Is.EqualTo(Color.Blue));
                Assert.That(colors.Skip(1).Take(colorCount - 1), Has.All.EqualTo(Color.Red));
                Assert.That(colors[colorCount], Is.EqualTo(new Color(0.25f, initialParameters.G, initialParameters.B, 0f)));
                applied = picker.FindControl<BoxContainer>("AppliedList").Children.OfType<ContainerButton>().Single();
            });

            await Click(pair, applied);
            await client.WaitAssertion(() =>
            {
                var editor = picker.FindControl<BoxContainer>("ColorEditor");
                Assert.That(editor.Children.OfType<ColorSelectorSliders>().First().Color, Is.EqualTo(Color.Blue));
                Assert.That(ParameterSliders(editor)[0].Value, Is.EqualTo(0.25f));
            });
        }
        finally
        {
            await client.WaitPost(() =>
            {
                picker?.Parent?.RemoveChild(picker);
                picker?.Dispose();
                localization.SetCulture(culture!);
            });
        }

        await pair.CleanReturnAsync();
    }

    [TestCase("Human")]
    [TestCase("Vulpkanin")]
    public async Task AddingGradientUsesItsDefaultParameters(string species)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var culture = localization.DefaultCulture;
        MarkingGridPicker picker = null;
        MarkingTile tile = null;
        MarkingSet added = null;

        try
        {
            await client.WaitPost(() =>
            {
                localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
                picker = CreatePicker(MarkingCategories.HairSpecial);
                picker.SetData(new List<Marking>(), species, Sex.Female, Color.Brown, Color.Black);
                picker.OnMarkingAdded += set => added = new MarkingSet(set);
                client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(picker);
                tile = picker.FindControl<Control>("Grid").Children.OfType<MarkingTile>()
                    .Single(entry => entry.MarkingId == "HairGradientRGB3Col");
            });
            await Click(pair, tile);
            await client.WaitAssertion(() =>
            {
                Assert.That(added, Is.Not.Null);
                var marking = added.Markings[MarkingCategories.HairSpecial].Single();
                Assert.That(marking.MarkingColors, Has.Count.EqualTo(4));
                Assert.That(marking.MarkingColors.Take(3), Has.All.EqualTo(Color.White));
                Assert.That(marking.MarkingColors[3], Is.EqualTo(Color.FromHex("#26bfd900")));
                Assert.That(ParameterSliders(picker.FindControl<BoxContainer>("ColorEditor")), Has.Length.EqualTo(3));
            });
        }
        finally
        {
            await client.WaitPost(() =>
            {
                picker?.Parent?.RemoveChild(picker);
                picker?.Dispose();
                localization.SetCulture(culture!);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static MarkingGridPicker CreatePicker(MarkingCategories category)
    {
        return new MarkingGridPicker
        {
            IgnoreCategories = string.Join(',', Enum.GetValues<MarkingCategories>().Where(value => value != category)),
            SetSize = new Vector2(1000, 600),
        };
    }

    private static Slider[] ParameterSliders(BoxContainer editor)
    {
        return editor.Children.OfType<BoxContainer>().SelectMany(row => row.Children).OfType<Slider>().ToArray();
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
}
