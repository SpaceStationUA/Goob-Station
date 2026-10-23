// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Server.Body.Components;
using Content.Server.Polymorph.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction.Components;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsMutationCleanupTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RepeatedNativePolymorphsPreserveInstability(bool foreignMutation)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        var expected = 0;
        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            mutations.ClearDormant(human);
#pragma warning disable RA0002 // Isolate the native polymorph from randomly assigned dormant mutations.
            entities.GetComponent<MutatableComponent>(human).Dormant.Add("MutationMonkified");
#pragma warning restore RA0002
            if (foreignMutation)
            {
                EntProtoId<MutationComponent> foreign = "MutationStimmed";
                Assert.That(mutations.AddMutation(human, foreign), Is.True);
                expected = mutations.AllMutations[foreign].Instability;
            }
            Assert.That(mutations.GetInstability(human), Is.EqualTo(expected));
        });

        for (var cycle = 0; cycle < 3; cycle++)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(mutations.AddMutation(human, "MutationMonkified"), Is.True);
                var monkey = entities.EntityQuery<PolymorphedEntityComponent>()
                    .Single(comp => comp.Parent == human).Owner;
                Assert.That(mutations.GetInstability(human), Is.Zero, "The original body must retain no transferred instability.");
                Assert.That(mutations.GetInstability(monkey), Is.EqualTo(expected));
                Assert.That(mutations.RemoveMutation(monkey, "MutationMonkified"), Is.True);
                Assert.That(mutations.GetInstability(human), Is.EqualTo(expected));
                Assert.That(mutations.HasMutation(human, "MutationMonkified"), Is.False);
                Assert.That(Enumerable.Contains(entities.GetComponent<MutatableComponent>(human).Dormant,
                    (EntProtoId<MutationComponent>) "MutationMonkified"), Is.True);
            });
            await pair.RunTicksSync(1);
        }

        await server.WaitAssertion(() =>
        {
            mutations.ClearMutations(human);
            Assert.That(mutations.GetInstability(human), Is.Zero);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("MobHuman")]
    [TestCase("MobMonkey")]
    public async Task CleansingCleverRestoresOnlyTheSubjectsOriginalAbilities(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity(prototype, map.GridCoords);
            var registrations = new[] { "ComplexInteraction", "BlockReading", "BlockWriting" }
                .Select(name => factory.GetRegistration(name)).ToArray();
            var present = registrations.Select(reg => entities.HasComponent(human, reg)).ToArray();
            entities.TryGetComponent<ComplexInteractionComponent>(human, out var originalInteraction);
            Assert.That(mutations.AddMutation(human, "MutationClever"), Is.True);
            Assert.That(entities.HasComponent(human, registrations[0]), Is.True);
            Assert.That(entities.HasComponent(human, registrations[1]), Is.False);
            Assert.That(entities.HasComponent(human, registrations[2]), Is.False);
            if (originalInteraction != null)
                Assert.That(entities.GetComponent<ComplexInteractionComponent>(human), Is.SameAs(originalInteraction));

            Assert.That(mutations.RemoveMutation(human, "MutationClever"), Is.True);
            Assert.That(registrations.Select(reg => entities.HasComponent(human, reg)), Is.EqualTo(present));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WebbingGrantsCraftingToTheHostAndPreservesNativeTags(bool nativeTag)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var tags = server.System<TagSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            if (nativeTag)
                tags.AddTag(human, "SpiderCraft");
            Assert.That(mutations.AddMutation(human, "MutationWebbingProduction"), Is.True);
            var mutation = mutations.GetMutation((human, entities.GetComponent<MutatableComponent>(human)), "MutationWebbingProduction")!.Value;
            Assert.That(tags.HasTag(human, "SpiderCraft"), Is.True);
            Assert.That(tags.HasTag(mutation.Owner, "SpiderCraft"), Is.False);
            Assert.That(mutations.RemoveMutation(human, "MutationWebbingProduction"), Is.True);
            Assert.That(tags.HasTag(human, "SpiderCraft"), Is.EqualTo(nativeTag));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AntennaCleanupRemovesGrantedRadioAndPreservesNativeRadio(bool nativeRadio)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            if (nativeRadio)
            {
                entities.AddComponent<IntrinsicRadioReceiverComponent>(human);
                var active = entities.AddComponent<ActiveRadioComponent>(human);
                active.Channels.Add("Science");
                active.ReceiveAllChannels = true;
                var transmitter = entities.AddComponent<IntrinsicRadioTransmitterComponent>(human);
                transmitter.Channels.Add("Science");
            }
            Assert.That(mutations.AddMutation(human, "MutationAntenna"), Is.True);
            Assert.That(entities.GetComponent<ActiveRadioComponent>(human).Channels,
                Does.Contain((ProtoId<RadioChannelPrototype>) "Common"));
            Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(human).Channels,
                Does.Contain((ProtoId<RadioChannelPrototype>) "Common"));
            Assert.That(mutations.RemoveMutation(human, "MutationAntenna"), Is.True);
            Assert.That(entities.HasComponent<ActiveRadioComponent>(human), Is.EqualTo(nativeRadio));
            Assert.That(entities.HasComponent<IntrinsicRadioTransmitterComponent>(human), Is.EqualTo(nativeRadio));
            Assert.That(entities.HasComponent<IntrinsicRadioReceiverComponent>(human), Is.EqualTo(nativeRadio));
            if (nativeRadio)
            {
                var active = entities.GetComponent<ActiveRadioComponent>(human);
                Assert.That(active.Channels.Select(id => id.ToString()), Is.EquivalentTo(new[] { "Science" }));
                Assert.That(active.ReceiveAllChannels, Is.True);
                Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(human).Channels.Select(id => id.ToString()),
                    Is.EquivalentTo(new[] { "Common", "Science" }));
            }
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(1f)]
    [TestCase(1.5f)]
    public async Task StimmedAcceleratesMetabolismAndRestoresTheOriginalInterval(float multiplier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var body = server.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var metabolizers = body.GetBodyOrgans(human)
                .Where(organ => entities.HasComponent<MetabolizerComponent>(organ.Id))
                .Select(organ => entities.GetComponent<MetabolizerComponent>(organ.Id)).ToArray();
            Assert.That(metabolizers, Is.Not.Empty);
            foreach (var metabolizer in metabolizers)
                metabolizer.UpdateIntervalMultiplier = multiplier;
            var intervals = metabolizers.Select(m => m.AdjustedUpdateInterval.TotalSeconds).ToArray();
            Assert.That(mutations.AddMutation(human, "MutationStimmed"), Is.True);
            for (var i = 0; i < metabolizers.Length; i++)
                Assert.That(metabolizers[i].AdjustedUpdateInterval.TotalSeconds, Is.EqualTo(intervals[i] / 1.25).Within(0.00001));
            Assert.That(mutations.RemoveMutation(human, "MutationStimmed"), Is.True);
            for (var i = 0; i < metabolizers.Length; i++)
                Assert.That(metabolizers[i].AdjustedUpdateInterval.TotalSeconds, Is.EqualTo(intervals[i]).Within(0.00001));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
