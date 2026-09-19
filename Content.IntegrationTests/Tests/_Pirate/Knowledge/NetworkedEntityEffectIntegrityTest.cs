// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class NetworkedEntityEffectIntegrityTest
{
    private static readonly HashSet<string> KnownBroken =
    [
        "CandyEgg", "CockatriceEgg", "CoralEgg", "DragonEgg", "DreamEgg", "KnightEgg", "MimeEgg",
        "PhoenixEgg", "PigeonEgg", "PoultrygeistEgg", "SickleEgg", "SnowEgg", "VoidEgg", "ZappyEgg",
    ];

    [Test]
    public async Task EffectsOnTriggerCanCrossTheWire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var serializer = server.ResolveDependency<IRobustSerializer>();
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            var offenders = new List<string>();
            var stillBroken = new HashSet<string>();

            foreach (var entity in server.ProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryGetComponent<EntityEffectOnTriggerComponent>(out var trigger, factory))
                    continue;

                foreach (var effect in trigger.Effects)
                {
                    if (serializer.CanSerialize(effect.GetType()))
                        continue;

                    if (KnownBroken.Contains(entity.ID))
                        stillBroken.Add(entity.ID);
                    else
                        offenders.Add($"{entity.ID} -> {effect.GetType().Name}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "These prototypes put an effect that NetSerializer does not know about into a " +
                "networked component, so every client that sees one will spam the server log:\n  " +
                string.Join("\n  ", offenders) +
                "\nFor installing brain chips on spawn use OrganChipsOnSpawn, which is not networked.");
        });

        await pair.CleanReturnAsync();
    }
}
