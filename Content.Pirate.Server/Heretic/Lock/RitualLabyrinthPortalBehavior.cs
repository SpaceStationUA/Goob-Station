// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Heretic.EntitySystems;
using Content.Shared.Heretic.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed partial class RitualLabyrinthPortalBehavior : RitualCustomBehavior
{
    [DataField]
    public EntProtoId Portal = "LabyrinthTear";

    public override bool Execute(RitualData args, out string? outstr)
    {
        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        var coordinates = args.EntityManager.GetComponent<TransformComponent>(args.Platform).Coordinates;
        var portal = args.EntityManager.SpawnEntity(Portal, coordinates);
        args.EntityManager.EnsureComponent<LabyrinthPortalComponent>(portal).HereticMind = args.Mind.Owner;
        args.EntityManager.System<GhoulSystem>().SetBoundHeretic(portal, args.Performer);
    }
}
