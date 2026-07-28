// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Heretic.Ritual;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Heretic.Prototypes;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed partial class RitualLockAscendBehavior : RitualSacrificeBehavior
{
    public override bool Execute(RitualData args, out string? outstr)
    {
        if (!base.Execute(args, out outstr))
            return false;

        uids = uids
            .Where(uid => !_body.GetBodyOrgans(uid)
                .Any(organ => args.EntityManager.HasComponent<HeartComponent>(organ.Id)))
            .ToList();

        if (uids.Count < Min)
        {
            outstr = Loc.GetString("heretic-ritual-fail-sacrifice-lock");
            return false;
        }

        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        base.Finalize(args);
    }
}
