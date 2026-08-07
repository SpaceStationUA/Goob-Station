// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Heretic.Lock;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Heretic.Prototypes;
using Robust.Server.Containers;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed partial class RitualEldritchIdBehavior : RitualCustomBehavior
{
    private EntityUid? _idCard;
    private EntityUid? _door;

    public override bool Execute(RitualData args, out string? outstr)
    {
        _idCard = null;
        _door = null;

        var lookup = args.EntityManager.System<EntityLookupSystem>();
        var containers = args.EntityManager.System<ContainerSystem>();

        foreach (var entity in lookup.GetEntitiesInRange(args.Platform, 1.5f))
        {
            if (containers.IsEntityInContainer(entity))
                continue;

            if (_idCard == null &&
                args.EntityManager.HasComponent<IdCardComponent>(entity) &&
                !args.EntityManager.HasComponent<EldritchIdCardComponent>(entity))
            {
                _idCard = entity;
                continue;
            }

            if (_door == null && args.EntityManager.HasComponent<DoorComponent>(entity))
                _door = entity;
        }

        if (_idCard == null)
        {
            outstr = Loc.GetString("heretic-ritual-fail-no-id-card");
            return false;
        }

        if (_door == null)
        {
            outstr = Loc.GetString("heretic-ritual-fail-items",
                ("itemlist", Loc.GetString("heretic-ritual-ingredient-door")));
            return false;
        }

        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        if (_idCard is { } idCard && args.EntityManager.EntityExists(idCard))
            args.EntityManager.EnsureComponent<EldritchIdCardComponent>(idCard);

        if (_door is { } door && args.EntityManager.EntityExists(door))
            args.EntityManager.QueueDeleteEntity(door);

        _idCard = null;
        _door = null;
    }
}
