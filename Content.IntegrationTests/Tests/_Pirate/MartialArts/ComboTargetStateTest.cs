// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ComboState = Content.Goobstation.Shared.MartialArts.Components.CanPerformComboComponent.CanPerformComboComponent_AutoState;

namespace Content.IntegrationTests.Tests._Pirate.MartialArts;

[TestFixture]
public sealed class ComboTargetStateTest
{
    [Test]
    public async Task DeletedTargetIsOmittedFromComboState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var user = entities.Spawn(null, MapCoordinates.Nullspace);
            var target = entities.Spawn(null, MapCoordinates.Nullspace);
            var combo = entities.EnsureComponent<CanPerformComboComponent>(user);
            combo.CurrentTarget = target;
            combo.LastAttacks.Add(ComboAttackType.Harm);
            combo.ConsecutiveGnashes = 2;

            ComboState ReadState() =>
                (ComboState) entities.GetComponentState(entities.EventBus, combo, null, default)!;

            Assert.That(ReadState().CurrentTarget, Is.EqualTo(entities.GetNetEntity(target)));
            entities.DeleteEntity(target);
            var state = ReadState();
            Assert.Multiple(() =>
            {
                Assert.That(state.CurrentTarget, Is.Null);
                Assert.That(state.LastAttacks, Is.EqualTo(new[] { ComboAttackType.Harm }));
                Assert.That(state.ConsecutiveGnashes, Is.EqualTo(2));
            });
            entities.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }
}
