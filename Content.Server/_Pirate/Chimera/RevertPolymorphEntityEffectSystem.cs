// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Chimera;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;

namespace Content.Server._Pirate.Chimera;

public sealed partial class RevertPolymorphEntityEffectSystem : SharedRevertPolymorphEntityEffectSystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    protected override void RevertPolymorph(EntityUid uid)
    {
        if (!HasComp<PolymorphedEntityComponent>(uid))
            return;

        EnsureComp<PolymorphableComponent>(uid);
        _polymorph.Revert(uid);
    }
}
