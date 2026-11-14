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
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

[TestFixture]
[TestOf(typeof(MarkingGridPicker))]
public sealed class MarkingCategoryLayoutTest
{
    [TestCase(640)]
    [TestCase(1000)]
    public async Task TwoCategoryRowsLeaveRoomForMarkingsAndScrollToTheLastCategory(int width)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var localization = client.ResolveDependency<ILocalizationManager>();
        var culture = localization.DefaultCulture;
        var size = new Vector2(width, 320);
        MarkingGridPicker picker = null!;
        ScrollContainer categories = null!;
        Button lastCategory = null!;

        try
        {
            await client.WaitPost(() =>
            {
                localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
                picker = new MarkingGridPicker
                {
                    IgnoreCategories = "Hair,FacialHair,HairSpecial,FacialHairSpecial",
                    SetSize = size,
                    HorizontalAlignment = Control.HAlignment.Left,
                    VerticalAlignment = Control.VAlignment.Top,
                };
                picker.SetData(new List<Marking>(), "Reptilian", Sex.Male, Color.White, Color.Black);
                client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(picker);
                categories = picker.FindControl<ScrollContainer>("CategoryScroll");
            });
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                Layout(picker, size);
                var buttons = picker.FindControl<Control>("CategoryChips").Children.OfType<Button>().ToArray();
                var rows = buttons.GroupBy(button => button.Position.Y).OrderBy(row => row.Key).ToArray();
                Assert.That(rows.Length, Is.GreaterThan(2), "this species must exercise category overflow");
                Assert.That(categories.Size.Y, Is.EqualTo(rows[1].Key + rows[1].Max(button => button.Size.Y)).Within(1f),
                    "exactly two complete rows fit in the category viewport");
                Assert.That(categories.Children.OfType<VScrollBar>().Single().Visible, Is.True);
                Assert.That(picker.FindControl<ScrollContainer>("Scroll").Size.Y, Is.GreaterThan(100f),
                    "the category list must leave usable space for choosing a marking");

                lastCategory = buttons.Last();
                categories.VScroll = picker.FindControl<Control>("CategoryChips").Size.Y;
                Layout(picker, size);
                AssertVisibleInside(lastCategory, categories);
            });

            await Click(pair, lastCategory);
            await pair.RunTicksSync(2);
            await client.WaitAssertion(() =>
            {
                Layout(picker, size);
                var selected = picker.FindControl<Control>("CategoryChips").Children.OfType<Button>().Last();
                Assert.That(selected.Pressed, Is.True, "a category reached by scrolling remains selectable");
                AssertVisibleInside(selected, categories);
                Assert.That(picker.FindControl<Control>("Grid").ChildCount, Is.GreaterThan(0));
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

    private static void Layout(Control control, Vector2 size)
    {
        // Flow containers can invalidate measurement once after receiving their arranged width.
        for (var i = 0; i < 2; i++)
        {
            control.Measure(size);
            control.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
        }
    }

    private static void AssertVisibleInside(Control child, Control viewport)
    {
        var top = child.GlobalPosition.Y - viewport.GlobalPosition.Y;
        Assert.That(top, Is.GreaterThanOrEqualTo(-1f));
        Assert.That(top + child.Size.Y, Is.LessThanOrEqualTo(viewport.Size.Y + 1f));
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
