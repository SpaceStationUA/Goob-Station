// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class TriggerOnDamageIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateTriggerOnDamageTestBase
  components:
  - type: Damageable
    damageContainer: Inorganic
  - type: TriggerOnDamage
    threshold: 5
  - type: DeleteOnTrigger

- type: entity
  parent: PirateTriggerOnDamageTestBase
  id: PirateTriggerOnDamageNeverTest
  components:
  - type: TriggerOnDamage
    probability: 0
";

    [Test]
    public async Task TriggerHonorsThresholdAndProbability()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var damageable = server.System<DamageableSystem>();
        var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
        EntityUid origin = default;
        EntityUid target = default;
        EntityUid never = default;

        await server.WaitAssertion(() =>
        {
            origin = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            target = entMan.SpawnEntity("PirateTriggerOnDamageTestBase", MapCoordinates.Nullspace);

            damageable.TryChangeDamage(target, new DamageSpecifier(blunt, 4), origin: origin);
            Assert.That(entMan.Deleted(target), Is.False, "Damage below the threshold triggered the entity.");

            damageable.TryChangeDamage(target, new DamageSpecifier(blunt, 5), origin: origin);
            Assert.That(entMan.Deleted(target), Is.False, "Damage equal to the threshold triggered the entity.");
        });

        await server.WaitPost(() =>
        {
            damageable.TryChangeDamage(target, new DamageSpecifier(blunt, 6), origin: origin);
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(target), Is.True, "Damage above the threshold did not trigger the entity.");
        });

        await server.WaitPost(() =>
        {
            never = entMan.SpawnEntity("PirateTriggerOnDamageNeverTest", MapCoordinates.Nullspace);
            damageable.TryChangeDamage(never, new DamageSpecifier(blunt, 6), origin: origin);
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(never), Is.False, "A zero-probability trigger activated.");

            entMan.DeleteEntity(never);
            entMan.DeleteEntity(origin);
        });

        await pair.CleanReturnAsync();
    }
}
