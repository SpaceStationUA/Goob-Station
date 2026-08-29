// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Pirate.Knowledge;
using Content.Client._Pirate.Knowledge.UI;
using Content.Client.Popups;
using Content.IntegrationTests.Pair;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeUiIntegrationTest
{
    [Test]
    public async Task ProfileEditorSupportsSelectionBudgetResetAndApplyWorkflow()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        KnowledgeProfileEditor editor = null!;
        KnowledgeProfile? applied = null;
        var applyCount = 0;
        Button firstAidDecrease = null!;
        Button firstAidIncrease = null!;
        Button chemistryDecrease = null!;
        Button chemistryIncrease = null!;
        Button saveButton = null!;
        Button resetButton = null!;
        BoxContainer skills = null!;
        Label pointsLabel = null!;

        await client.WaitAssertion(() =>
        {
            editor = new KnowledgeProfileEditor();
            editor.OnApply += profile =>
            {
                applied = profile;
                applyCount++;
            };
            editor.SetProfile("Human", new KnowledgeProfile());
            saveButton = FindNamed<Button>(editor, "SaveButton");
            resetButton = FindNamed<Button>(editor, "ResetButton");
            skills = FindNamed<BoxContainer>(editor, "Skills");
            pointsLabel = FindNamed<Label>(editor, "PointsLabel");

            Assert.Multiple(() =>
            {
                Assert.That(skills.ChildCount, Is.GreaterThan(0));
                Assert.That(saveButton.Disabled, Is.True);
                Assert.That(resetButton.Disabled, Is.True);
            });

            (firstAidDecrease, firstAidIncrease) = GetSkillButtons(client.ProtoMan, skills, "FirstAidKnowledge");
            (chemistryDecrease, chemistryIncrease) = GetSkillButtons(client.ProtoMan, skills, "ChemistryKnowledge");
        });

        await Click(pair, firstAidIncrease);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(saveButton.Disabled, Is.False);
                Assert.That(resetButton.Disabled, Is.False);
                Assert.That(pointsLabel.Text, Does.Contain("9"));
                Assert.That(firstAidDecrease.Disabled, Is.False);
            });
        });

        await Click(pair, saveButton);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(applyCount, Is.EqualTo(1));
                Assert.That(applied?.Mastery["FirstAidKnowledge"], Is.EqualTo(1));
                Assert.That(saveButton.Disabled, Is.True);
            });

            applied!.Value.Mastery["FirstAidKnowledge"] = 3;
        });

        await Click(pair, firstAidIncrease);
        await Click(pair, saveButton);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(applyCount, Is.EqualTo(2));
                Assert.That(applied?.Mastery["FirstAidKnowledge"], Is.EqualTo(2),
                    "Mutating a previously applied profile must not mutate the editor's working copy.");
            });
        });

        await Click(pair, resetButton);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(saveButton.Disabled, Is.False);
                Assert.That(pointsLabel.Text, Does.Contain("10"));
            });
            (firstAidDecrease, firstAidIncrease) = GetSkillButtons(client.ProtoMan, skills, "FirstAidKnowledge");
            (chemistryDecrease, chemistryIncrease) = GetSkillButtons(client.ProtoMan, skills, "ChemistryKnowledge");
        });
        await Click(pair, saveButton);
        await client.WaitAssertion(() =>
        {
            Assert.That(applied?.Mastery, Is.Empty);
            Assert.That(applyCount, Is.EqualTo(3));
        });

        await Click(pair, firstAidIncrease);
        await Click(pair, firstAidIncrease);
        await Click(pair, firstAidIncrease);
        await client.WaitAssertion(() => Assert.That(firstAidIncrease.Disabled, Is.True,
            "The editor must stop at the skill's maximum selectable mastery."));

        await Click(pair, chemistryIncrease);
        await Click(pair, chemistryIncrease);
        await Click(pair, chemistryIncrease);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(pointsLabel.Text, Does.Contain("-2"));
                Assert.That(pointsLabel.FontColorOverride, Is.EqualTo(Color.Red));
                Assert.That(saveButton.Disabled, Is.True,
                    "An over-budget profile must never be applicable.");
            });
        });

        await Click(pair, chemistryDecrease);
        await client.WaitAssertion(() =>
        {
            Assert.That(pointsLabel.Text, Does.Contain("1"));
            Assert.That(saveButton.Disabled, Is.False);
        });
        await Click(pair, saveButton);

        await client.WaitAssertion(() =>
        {
            editor.SetProfile((ProtoId<SpeciesPrototype>) "PirateMissingSpecies", new KnowledgeProfile());
            Assert.Multiple(() =>
            {
                Assert.That(skills.ChildCount, Is.Zero);
                Assert.That(saveButton.Disabled, Is.True);
                Assert.That(resetButton.Disabled, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CharacterTabShowsOnlyVisibleKnowledgeWithCategoryIconAndProgress()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var knowledge = client.System<SharedKnowledgeSystem>();
            var holder = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var tab = new KnowledgeTab();
            var placeholder = FindNamed<Label>(tab, "KnowledgePlaceholder");
            var knowledgeBox = FindNamed<BoxContainer>(tab, "KnowledgeBox");

            tab.UpdateKnowledgeTab(holder);
            Assert.Multiple(() =>
            {
                Assert.That(placeholder.Visible, Is.True);
                Assert.That(knowledgeBox.ChildCount, Is.Zero);
            });

            var store = knowledge.EnsureKnowledgeContainer(holder);
            tab.UpdateKnowledgeTab(holder);
            Assert.That(placeholder.Visible, Is.True,
                "An existing but empty knowledge store must still show the placeholder.");

            var firstAid = knowledge.EnsureKnowledge(store, "FirstAidKnowledge", 50, popup: false);
            var surgery = knowledge.EnsureKnowledge(store, "SurgeryKnowledge", 25, popup: false);
            var fabrication = knowledge.EnsureKnowledge(store, "FabricationKnowledge", 75, popup: false);
            var hidden = knowledge.EnsureKnowledge(store, "ChemistryKnowledge", 100, popup: false);
            Assert.That(firstAid, Is.Not.Null);
            Assert.That(surgery, Is.Not.Null);
            Assert.That(fabrication, Is.Not.Null);
            Assert.That(hidden, Is.Not.Null);

            firstAid!.Value.Comp.Experience = 7;
            firstAid.Value.Comp.Sprite = new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"));
            hidden!.Value.Comp.Hidden = true;

            tab.UpdateKnowledgeTab(holder);

            var categories = knowledgeBox.Children.OfType<BoxContainer>().ToList();
            var categoryLabels = categories
                .Select(category => ((PanelContainer) category.Children.First()).Children.OfType<Label>().Single())
                .ToList();
            var rows = categories
                .SelectMany(category => ((BoxContainer) category.Children.ElementAt(1)).Children
                    .OfType<PanelContainer>())
                .ToList();
            var names = rows.Select(GetSkillName).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(placeholder.Visible, Is.False);
                Assert.That(categoryLabels, Has.Count.EqualTo(2));
                Assert.That(rows, Has.Count.EqualTo(3));
                Assert.That(names, Is.EquivalentTo(new[]
                {
                    entMan.GetComponent<MetaDataComponent>(firstAid.Value.Owner).EntityName,
                    entMan.GetComponent<MetaDataComponent>(surgery!.Value.Owner).EntityName,
                    entMan.GetComponent<MetaDataComponent>(fabrication!.Value.Owner).EntityName,
                }));
                Assert.That(names, Does.Not.Contain(
                    entMan.GetComponent<MetaDataComponent>(hidden.Value.Owner).EntityName));
            });

            var firstAidRow = rows.Single(row => GetSkillName(row) ==
                entMan.GetComponent<MetaDataComponent>(firstAid.Value.Owner).EntityName);
            var body = (BoxContainer) firstAidRow.Children.Single();
            var summary = (BoxContainer) body.Children.First();
            var labels = summary.Children.OfType<BoxContainer>().Single().Children.OfType<Label>().ToArray();
            var progress = body.Children.OfType<ProgressBar>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(summary.Children.OfType<TextureRect>().Single().Texture, Is.Not.Null);
                Assert.That(labels[0].Text, Is.EqualTo(knowledge.GetKnowledgeInfo(firstAid.Value).Name));
                Assert.That(labels[1].Text, Does.Contain("50"));
                Assert.That(labels[1].Text, Does.Contain(SharedKnowledgeSystem.GetMasteryString(2)));
                Assert.That(progress.MinValue, Is.Zero);
                Assert.That(progress.MaxValue, Is.EqualTo(19));
                Assert.That(progress.Value, Is.EqualTo(7));
            });

            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminWindowFiltersResetsAndSubmitsOnlyChangedSkillProgress()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        KnowledgeAdminWindow window = null!;
        Dictionary<string, KnowledgeAdminEdit>? applied = null;
        var refreshes = 0;
        BoxContainer skills = null!;
        LineEdit search = null!;
        ScrollContainer scroll = null!;
        Label empty = null!;
        Button apply = null!;
        Button reset = null!;
        TextureButton refresh = null!;
        SpinBox fabricationLevel = null!;
        SpinBox fabricationExperience = null!;
        SpinBox firstAidLevel = null!;
        SpinBox firstAidExperience = null!;

        await client.WaitAssertion(() =>
        {
            window = new KnowledgeAdminWindow();
            window.ApplyRequested += changes => applied = changes;
            window.RefreshRequested += () => refreshes++;
            window.SetState(new KnowledgeAdminEuiState(
                new NetEntity(1),
                "Test subject",
                [
                    new KnowledgeAdminEntry(
                        "FirstAidKnowledge",
                        "First aid",
                        "Medical description",
                        "Medical",
                        false,
                        true,
                        10,
                        5,
                        3,
                        20),
                    new KnowledgeAdminEntry(
                        "FabricationKnowledge",
                        "Fabrication",
                        "Crafting description",
                        "Crafting",
                        true,
                        false,
                        0,
                        0,
                        0,
                        19),
                ]));

            skills = FindNamed<BoxContainer>(window, "SkillsBox");
            search = FindNamed<LineEdit>(window, "SearchInput");
            scroll = FindNamed<ScrollContainer>(window, "SkillsScroll");
            empty = FindNamed<Label>(window, "EmptyLabel");
            apply = FindNamed<Button>(window, "ApplyButton");
            reset = FindNamed<Button>(window, "ResetButton");
            refresh = FindNamed<TextureButton>(window, "RefreshButton");
            var spins = FindControls<SpinBox>(skills).ToList();
            fabricationLevel = spins[0];
            fabricationExperience = spins[1];
            firstAidLevel = spins[2];
            firstAidExperience = spins[3];

            Assert.Multiple(() =>
            {
                Assert.That(skills.ChildCount, Is.EqualTo(2));
                Assert.That(spins, Has.Count.EqualTo(4));
                Assert.That(apply.Disabled, Is.True);
                Assert.That(reset.Disabled, Is.True);
                Assert.That(FindControls<Label>(skills).Any(label => label.Text.Contains("15")), Is.True,
                    "The summary must show learned plus temporary level.");
            });

            fabricationLevel.Value = 42;
            fabricationExperience.Value = 18;
            Assert.Multiple(() =>
            {
                Assert.That(apply.Disabled, Is.False);
                Assert.That(reset.Disabled, Is.False);
            });
        });

        await Click(pair, reset);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(fabricationLevel.Value, Is.Zero);
                Assert.That(fabricationExperience.Value, Is.Zero);
                Assert.That(apply.Disabled, Is.True);
            });

            fabricationLevel.Value = 42;
            fabricationExperience.Value = 18;
        });

        await Click(pair, apply);
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(applied, Has.Count.EqualTo(1));
                Assert.That(applied?["FabricationKnowledge"], Is.EqualTo(new KnowledgeAdminEdit(42, 18)));
                Assert.That(apply.Disabled, Is.True, "Apply must debounce until authoritative state returns.");
            });

            search.SetText("definitely missing", invokeEvent: true);
            Assert.Multiple(() =>
            {
                Assert.That(scroll.Visible, Is.False);
                Assert.That(empty.Visible, Is.True);
            });
            search.SetText("FirstAidKnowledge", invokeEvent: true);
            Assert.Multiple(() =>
            {
                Assert.That(scroll.Visible, Is.True);
                Assert.That(skills.Children.Count(control => control.Visible), Is.EqualTo(1));
            });

            firstAidLevel.Value = 100;
            Assert.Multiple(() =>
            {
                Assert.That(firstAidExperience.Value, Is.Zero);
                Assert.That(firstAidExperience.LineEditDisabled, Is.True);
            });
        });

        await Click(pair, refresh);
        await client.WaitAssertion(() =>
        {
            Assert.That(refreshes, Is.EqualTo(1));
            window.Dispose();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClientStateRefreshAndSkillPopupRespectConfigurationAndCooldown()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var changed = 0;
        EntityUid serverSkill = default;
        Action onChanged = () => changed++;

        await client.WaitAssertion(() =>
        {
            var knowledge = client.System<KnowledgeClientSystem>();
            knowledge.KnowledgeChanged += onChanged;
        });

        await server.WaitPost(() =>
        {
            serverSkill = server.EntMan.SpawnEntity("FabricationKnowledge", map.GridCoords);
            var component = server.EntMan.GetComponent<KnowledgeComponent>(serverSkill);
            component.LearnedLevel = 1;
            server.EntMan.Dirty(serverSkill, component);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientSkill = pair.ToClientUid(serverSkill);
            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.GreaterThanOrEqualTo(1),
                    "Receiving an initial networked skill state must notify open character UI.");
                Assert.That(client.EntMan.GetComponent<KnowledgeComponent>(clientSkill).LearnedLevel, Is.EqualTo(1));
            });
            changed = 0;
        });

        await server.WaitPost(() =>
        {
            var component = server.EntMan.GetComponent<KnowledgeComponent>(serverSkill);
            component.LearnedLevel = 2;
            server.EntMan.Dirty(serverSkill, component);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientSkill = pair.ToClientUid(serverSkill);
            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.GreaterThanOrEqualTo(1),
                    "Receiving a changed networked skill state must notify open character UI.");
                Assert.That(client.EntMan.GetComponent<KnowledgeComponent>(clientSkill).LearnedLevel, Is.EqualTo(2));
            });

            var popup = client.System<PopupSystem>();

            client.CfgMan.SetCVar(KnowledgeCVars.SkillPopups, false);
            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new SkillPopupEvent("pirate-hidden-skill-popup"));
            Assert.That(popup.CursorLabels.Any(label => label.Text.Contains("pirate-hidden-skill-popup")), Is.False);

            client.CfgMan.SetCVar(KnowledgeCVars.SkillPopups, true);
            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new SkillPopupEvent("pirate-first-skill-popup"));
            Assert.That(popup.CursorLabels.Any(label => label.Text.Contains("pirate-first-skill-popup")), Is.True);

            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new SkillPopupEvent("pirate-throttled-skill-popup"));
            Assert.That(popup.CursorLabels.Any(label => label.Text.Contains("pirate-throttled-skill-popup")), Is.False,
                "Back-to-back skill messages must be throttled.");
        });

        await pair.RunSeconds(3.1f);
        await client.WaitAssertion(() =>
        {
            var popup = client.System<PopupSystem>();
            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new SkillPopupEvent("pirate-after-cooldown-popup"));
            Assert.That(popup.CursorLabels.Any(label => label.Text.Contains("pirate-after-cooldown-popup")), Is.True,
                "A popup must be accepted again after the cooldown.");

            client.CfgMan.SetCVar(KnowledgeCVars.SkillPopups, true);
            client.System<KnowledgeClientSystem>().KnowledgeChanged -= onChanged;
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(serverSkill));
        await pair.RunTicksSync(1);

        await pair.CleanReturnAsync();
    }

    private static (Button Decrease, Button Increase) GetSkillButtons(
        IPrototypeManager prototypes,
        BoxContainer skills,
        EntProtoId id)
    {
        var expectedName = prototypes.Index<EntityPrototype>(id).Name;
        var row = skills.Children
            .OfType<BoxContainer>()
            .Single(control => control.Children.FirstOrDefault() is Label label && label.Text == expectedName);
        var children = row.Children.ToArray();
        return ((Button) children[1], (Button) children[3]);
    }

    private static string GetSkillName(PanelContainer row)
    {
        var body = (BoxContainer) row.Children.Single();
        var summary = (BoxContainer) body.Children.First();
        return summary.Children.OfType<BoxContainer>().Single().Children.OfType<Label>().First().Text;
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T typed)
            yield return typed;

        foreach (var child in root.Children)
        {
            foreach (var control in FindControls<T>(child))
                yield return control;
        }
    }

    private static T FindNamed<T>(Control root, string name) where T : Control
    {
        if (TryFindNamed(root, name, out T control))
            return control;

        throw new InvalidOperationException($"Could not find {typeof(T).Name} named {name}.");
    }

    private static bool TryFindNamed<T>(Control root, string name, out T found) where T : Control
    {
        if (root is T typed && root.Name == name)
        {
            found = typed;
            return true;
        }

        foreach (var child in root.Children)
        {
            if (TryFindNamed(child, name, out found))
                return true;
        }

        found = null!;
        return false;
    }

    private static async Task Click(TestPair pair, BaseButton button)
    {
        await pair.Client.WaitPost(() =>
        {
            button.Mode = BaseButton.ActionMode.Press;
            button.MuteSounds = true;
        });

        var screen = new ScreenCoordinates(Vector2.Zero, default);
        var down = new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Down,
            screen,
            false,
            Vector2.Zero,
            Vector2.Zero);
        await pair.Client.DoGuiEvent(button, down);

        var up = new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Up,
            screen,
            false,
            Vector2.Zero,
            Vector2.Zero);
        await pair.Client.DoGuiEvent(button, up);
        await pair.RunTicksSync(1);
    }
}
