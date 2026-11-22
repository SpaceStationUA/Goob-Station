// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;

namespace Content.Pirate.Server.Projectiles;

public sealed class ProjectileDeletedReferenceSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var dirty = false;

            if (comp.Shooter is { } shooter && TerminatingOrDeleted(shooter))
            {
                comp.Shooter = null;
                dirty = true;
            }

            if (comp.Weapon is { } weapon && TerminatingOrDeleted(weapon))
            {
                comp.Weapon = null;
                dirty = true;
            }

            if (dirty)
                Dirty(uid, comp);
        }
    }
}
