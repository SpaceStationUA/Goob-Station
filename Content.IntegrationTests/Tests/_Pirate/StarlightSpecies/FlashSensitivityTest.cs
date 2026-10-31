using Content.Shared._Pirate.StarlightSpecies;
using Content.Shared.Flash;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using OldStatusEffectsSystem = Content.Shared.StatusEffect.StatusEffectsSystem;
using StatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class FlashSensitivityTest
{
    [TestCase("MobNeoKin", true, 2f)]
    [TestCase("MobNeoKin", false, 2f)]
    [TestCase("MobNeoResomi", true, 2f)]
    [TestCase("MobNeoResomi", false, 2f)]
    [TestCase("MobHuman", true, 1f)]
    [TestCase("MobHuman", false, 1f)]
    public async Task SensitivityExtendsBlindnessWithoutExtendingParalysis(string prototype, bool stun, float modifier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var mob = em.SpawnEntity(prototype, MapCoordinates.Nullspace);
            if (modifier != 1f)
                Assert.That(em.GetComponent<FlashModifierComponent>(mob).Modifier, Is.EqualTo(modifier));
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            em.System<SharedFlashSystem>().Flash(mob, null, null, TimeSpan.FromSeconds(2), 0.5f,
                displayPopup: false, stunDuration: stun ? TimeSpan.FromSeconds(1) : null);

            Assert.That(em.System<OldStatusEffectsSystem>().TryGetTime(mob, "Flashed", out var blindness), Is.True);
            Assert.That(blindness!.Value.Item2 - now, Is.EqualTo(TimeSpan.FromSeconds(2 * modifier)));
            var effect = stun ? SharedStunSystem.StunId : MovementModStatusSystem.FlashSlowdown;
            Assert.That(em.System<StatusEffectsSystem>().TryGetTime(mob, effect, out var duration), Is.True);
            Assert.That(duration.EndEffectTime - now, Is.EqualTo(TimeSpan.FromSeconds(stun ? 1 : 2 * modifier)));
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }
}
