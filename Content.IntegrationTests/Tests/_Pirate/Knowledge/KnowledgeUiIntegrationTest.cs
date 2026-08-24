// SPDX-License-Identifier: AGPL-3.0-or-later

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
                Assert.That(pointsLabel.Text, Does.Contain("5"));
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
                Assert.That(pointsLabel.Text, Does.Contain("6"));
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
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(pointsLabel.Text, Does.Contain("-1"));
                Assert.That(pointsLabel.FontColorOverride, Is.EqualTo(Color.Red));
                Assert.That(saveButton.Disabled, Is.True,
                    "An over-budget profile must never be applicable.");
            });
        });

        await Click(pair, chemistryDecrease);
        await client.WaitAssertion(() => Assert.That(saveButton.Disabled, Is.False));
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

            var categoryLabels = knowledgeBox.Children.OfType<Label>().ToList();
            var rows = knowledgeBox.Children.OfType<BoxContainer>().ToList();
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
            var rowChildren = firstAidRow.Children.ToArray();
            var labels = ((BoxContainer) rowChildren[1]).Children.OfType<Label>().ToArray();
            var progress = (ProgressBar) rowChildren[2];
            Assert.Multiple(() =>
            {
                Assert.That(((TextureRect) rowChildren[0]).Texture, Is.Not.Null);
                Assert.That(labels[1].Text, Is.EqualTo(knowledge.GetKnowledgeInfo(firstAid.Value).Level));
                Assert.That(progress.MinValue, Is.Zero);
                Assert.That(progress.MaxValue, Is.EqualTo(19));
                Assert.That(progress.Value, Is.EqualTo(7));
            });

            entMan.DeleteEntity(holder);
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

    private static string GetSkillName(BoxContainer row)
        => ((BoxContainer) row.Children.ElementAt(1)).Children.OfType<Label>().First().Text;

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
