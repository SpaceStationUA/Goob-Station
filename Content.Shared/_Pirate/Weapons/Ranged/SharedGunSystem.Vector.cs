// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    public void ChamberVectorMagazine(EntityUid uid, bool playSound)
    {
        if (!TryComp<ChamberMagazineAmmoProviderComponent>(uid, out var provider))
            return;

        if (GetChamberEntity(uid) == null)
        {
            provider.BoltClosed = true;
            Dirty(uid, provider);
            Appearance.SetData(uid, AmmoVisuals.BoltClosed, true);
            _slots.SetLock(uid, ChamberSlot, true);
            CycleCartridge(uid, provider);
        }

        // Container messages have no actor to exclude from PVS; play sound only on the server.
        if (playSound && _netManager.IsServer)
        {
            var sound = provider.RackSound;
            Timer.Spawn(300, () =>
            {
                if (!TerminatingOrDeleted(uid))
                    Audio.PlayPvs(sound, uid);
            });
        }
    }
}
