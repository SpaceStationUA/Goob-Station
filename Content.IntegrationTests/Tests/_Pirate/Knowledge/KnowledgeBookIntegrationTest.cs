// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Pirate.Knowledge;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeBookIntegrationTest : InteractionTest
{
    protected override string PlayerPrototype => "PirateKnowledgeBookTestMob";

    public override async Task Setup()
    {
        await base.Setup();
        await Server.WaitPost(() => SEntMan.System<SharedKnowledgeSystem>().EnsureKnowledgeContainer(SPlayer));
    }

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  parent: InteractionTestMob
  id: PirateKnowledgeBookTestMob
  components:
  - type: KnowledgeHolder

- type: entity
  parent: BaseItem
  id: PirateKnowledgeSingleUseTestBook
  components:
  - type: KnowledgeGrantOnUse
    instant: true
    singleUse: true
    ash: Ash
    skills:
      LiteracyKnowledge: 100

- type: entity
  parent: BaseItem
  id: PirateKnowledgeEverythingTestBook
  components:
  - type: KnowledgeGrantOnUse
    instant: true
    singleUse: false
    grantEverything: true
";

    [Test]
    public async Task RepeatableSkillbookCancellationAndLearningCapWork()
    {
        var knowledge = SEntMan.System<SharedKnowledgeSystem>();

        await PlaceInHands("PirateBookFabrication");
        var book = HandSys.GetActiveItem((SPlayer, Hands));
        Assert.That(book, Is.Not.Null);

        await UseInHand();
        await RunTicks(1);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await CancelDoAfters();
        Assert.That(knowledge.GetKnowledge(SPlayer, "FabricationKnowledge"), Is.Null,
            "Cancelling the reading DoAfter must not grant a skill or XP.");
        Assert.That(SEntMan.Deleted(book!.Value), Is.False);

        await UseInHand();
        await RunTicks(1);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await AwaitDoAfters();

        var learned = knowledge.GetKnowledge(SPlayer, "FabricationKnowledge");
        Assert.That(learned, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(learned!.Value.Comp.LearnedLevel, Is.EqualTo(10),
                "The repeatable book must stop exactly at its configured level cap.");
            Assert.That(SEntMan.Deleted(book.Value), Is.False,
                "Normal skillbooks are reusable and must not be consumed.");
            Assert.That(ActiveDoAfters, Is.Empty,
                "Reading must stop repeating once no configured skill can gain more levels.");
        });
    }

    [Test]
    public async Task InstantGrantEverythingBookGrantsEveryCompatibleCatalogSkill()
    {
        var knowledge = SEntMan.System<SharedKnowledgeSystem>();

        await PlaceInHands("PirateKnowledgeEverythingTestBook");
        var book = HandSys.GetActiveItem((SPlayer, Hands));
        await UseInHand();
        await RunTicks(2);

        var missing = knowledge.AllKnowledges.Keys
            .Where(id => knowledge.GetKnowledge(SPlayer, id)?.Comp.LearnedLevel != 100)
            .Select(id => id.Id)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty,
                "GrantEverything must grant every skill from every explicit catalog.");
            Assert.That(knowledge.GetKnowledge(SPlayer, "PirateKnowledgeConflictSkill"), Is.Not.Null,
                "The test conflict skill proves that grant-everything also consumes auxiliary catalogs.");
            Assert.That(knowledge.GetKnowledge(SPlayer, "KnowledgeWeaponsEnergy")?.Comp.LearnedLevel, Is.EqualTo(100),
                "GrantEverything must finish with even normally conflicting catalog entries granted.");
            Assert.That(book, Is.Not.Null);
            Assert.That(SEntMan.Deleted(book!.Value), Is.False);
        });
    }

    [Test]
    public async Task InstantSingleUseBookGrantsSkillAndProducesAsh()
    {
        var knowledge = SEntMan.System<SharedKnowledgeSystem>();

        await PlaceInHands("PirateKnowledgeSingleUseTestBook");
        var book = HandSys.GetActiveItem((SPlayer, Hands));
        Assert.That(book, Is.Not.Null);

        await UseInHand();
        await RunTicks(3);

        var learned = knowledge.GetKnowledge(SPlayer, "LiteracyKnowledge");
        Assert.That(learned, Is.Not.Null);
        Assert.That(learned!.Value.Comp.LearnedLevel, Is.EqualTo(100));
        Assert.That(SEntMan.Deleted(book!.Value), Is.True,
            "A single-use instant book must consume itself after granting knowledge.");

        var ash = await FindEntity(("Ash", 1));
        Assert.That(SEntMan.Deleted(ash), Is.False,
            "Consuming a single-use book must spawn its configured ash entity.");
    }
}
