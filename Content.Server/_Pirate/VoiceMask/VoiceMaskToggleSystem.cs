// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.VoiceMask;
using Content.Shared._Pirate.VoiceMask;

namespace Content.Server._Pirate.VoiceMask;

public sealed class VoiceMaskToggleSystem : SharedVoiceMaskToggleSystem
{
    [Dependency] private VoiceMaskSystem _voiceMask = default!;

    protected override void Toggle(Entity<VoiceMaskToggleComponent> ent, EntityUid user)
    {
        if (!TryComp<VoiceMaskComponent>(ent, out var mask))
            return;

        _voiceMask.SetActive((ent, mask), !mask.Active, user);
    }
}
