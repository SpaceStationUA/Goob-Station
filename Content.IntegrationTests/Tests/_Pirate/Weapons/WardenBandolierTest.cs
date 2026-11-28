// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Weapons;

[TestFixture]
public sealed class WardenBandolierTest
{
    [Test]
    public async Task ReinsertStartingShell()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var belt = em.SpawnEntity("ClothingBeltBandolierHighCaliberFilled", map.GridCoords);
            var provider = em.GetComponent<BallisticAmmoProviderComponent>(belt);
            var gun = server.System<SharedGunSystem>();
            Assert.That(gun.GetAmmoCount(belt), Is.EqualTo(14));
            var take = new TakeAmmoEvent(1, [], map.GridCoords, null);
            em.EventBus.RaiseLocalEvent(belt, take);
            var shell = take.Ammo[0].Entity!.Value;
            Assert.That(em.GetComponent<MetaDataComponent>(shell).EntityPrototype!.ID, Is.EqualTo("ShellShotgunHighCaliber"));
            Assert.That(gun.TryBallisticInsert((belt, provider), shell, null), Is.True);
            Assert.That(gun.GetAmmoCount(belt), Is.EqualTo(14));
            var empty = em.SpawnEntity("ClothingBeltBandolier", map.GridCoords);
            Assert.That(gun.TryBallisticInsert((empty, em.GetComponent<BallisticAmmoProviderComponent>(empty)),
                em.SpawnEntity("ShellShotgun", map.GridCoords), null), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
