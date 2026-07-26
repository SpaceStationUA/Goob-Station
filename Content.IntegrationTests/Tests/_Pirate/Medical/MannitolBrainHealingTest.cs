// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.EntityEffects.Effects;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Medical;

[TestFixture]
public sealed class MannitolBrainHealingTest
{
    private static readonly ProtoId<ReagentPrototype> MannitolProto = "Mannitol";

    /// <summary>
    /// Organ integrity is the clamped sum of the organ's integrity modifiers
    /// (see TraumaSystem.UpdateOrganIntegrity), so a single small modifier lowers
    /// integrity to its value and raising the sum heals the organ back to its cap.
    /// This guards the arithmetic AdjustOrganIntegrity relies on.
    /// </summary>
    [Test]
    public async Task AdjustOrganIntegrityHealsBrainOnly()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<SharedBodySystem>();
        var trauma = entMan.System<TraumaSystem>();
        var effects = entMan.System<SharedEntityEffectsSystem>();

        var human = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            human = entMan.Spawn("MobHuman");
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var organs = body.GetBodyOrgans(human).ToList();
            var (brainId, brain) = organs.Single(o => o.Component.SlotId == "brain");
            var (eyesId, eyes) = organs.Single(o => o.Component.SlotId == "eyes");

            // Damage both organs the way live systems do: one small integrity modifier each.
            Assert.That(trauma.TryCreateOrganDamageModifier(brainId, 5, human, "TestDamage", brain));
            Assert.That(trauma.TryCreateOrganDamageModifier(eyesId, 5, human, "TestDamage", eyes));
            Assert.Multiple(() =>
            {
                Assert.That(brain.OrganIntegrity, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(eyes.OrganIntegrity, Is.EqualTo(FixedPoint2.New(5)));
            });

            var effect = new AdjustOrganIntegrity
            {
                Amount = FixedPoint2.New(4),
                SlotId = "brain",
            };

            effects.ApplyEffect(human, effect);
            Assert.That(brain.OrganIntegrity, Is.EqualTo(FixedPoint2.New(9)),
                "Brain integrity should rise by the effect amount");

            effects.ApplyEffect(human, effect);
            effects.ApplyEffect(human, effect);
            Assert.Multiple(() =>
            {
                Assert.That(brain.OrganIntegrity, Is.EqualTo(brain.IntegrityCap),
                    "Brain integrity should be capped at full");
                Assert.That(eyes.OrganIntegrity, Is.EqualTo(FixedPoint2.New(5)),
                    "Organs outside the target slot should not be healed");
            });

            // Extra applications on a healthy brain must be a no-op.
            effects.ApplyEffect(human, effect);
            Assert.That(brain.OrganIntegrity, Is.EqualTo(brain.IntegrityCap));

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Mannitol's metabolism must include the brain-healing effect,
    /// otherwise the reagent is a placebo again.
    /// </summary>
    [Test]
    public async Task MannitolHealsBrain()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var protoMan = pair.Server.ProtoMan;
        var mannitol = protoMan.Index(MannitolProto);

        Assert.That(mannitol.Metabolisms, Is.Not.Null);
        var healsBrain = mannitol.Metabolisms!.Values
            .SelectMany(entry => entry.Effects)
            .OfType<AdjustOrganIntegrity>()
            .Any(effect => effect.SlotId == "brain" && effect.Amount > FixedPoint2.Zero);

        Assert.That(healsBrain, Is.True, "Mannitol must heal brain organ integrity");

        await pair.CleanReturnAsync();
    }
}
