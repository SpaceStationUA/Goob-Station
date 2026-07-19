// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Random.Rules;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Ambience.Areas;

// Pirate: Trauma resolves these through its map area system; Pirate keeps the same rule data
// and approximates it with nearby area marker entities so the port stays self-contained.
public sealed partial class InAreaRule : RulesRule
{
    [DataField(required: true)]
    public List<EntProtoId> Areas = new();

    [DataField]
    public float Range = 12f;

    private readonly HashSet<Entity<AreaComponent>> _areaMarkers = new();

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (!TryGetPosition(entManager, uid, out var xform, out var worldPos))
            return false;

        var lookup = entManager.System<EntityLookupSystem>();
        _areaMarkers.Clear();
        lookup.GetEntitiesInRange(xform.MapID, worldPos, Range, _areaMarkers);

        foreach (var area in _areaMarkers)
        {
            var prototype = entManager.GetComponent<MetaDataComponent>(area.Owner).EntityPrototype;
            if (prototype == null || !Areas.Contains(new EntProtoId(prototype.ID)))
                continue;

            return !Inverted;
        }

        return Inverted;
    }

    private static bool TryGetPosition(EntityManager entManager, EntityUid uid, out TransformComponent xform, out Vector2 worldPos)
    {
        var xformQuery = entManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(uid, out xform!) || xform.MapUid == null)
        {
            worldPos = default;
            return false;
        }

        worldPos = entManager.System<SharedTransformSystem>().GetWorldPosition(xform, xformQuery);
        return true;
    }
}

public sealed partial class InDepartmentAreaRule : RulesRule
{
    [DataField(required: true)]
    public ProtoId<DepartmentPrototype> Department;

    [DataField]
    public float Range = 12f;

    private readonly HashSet<Entity<DepartmentAreaComponent>> _areaMarkers = new();

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        var xformQuery = entManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(uid, out var xform) || xform.MapUid == null)
            return false;

        var transform = entManager.System<SharedTransformSystem>();
        var lookup = entManager.System<EntityLookupSystem>();
        var worldPos = transform.GetWorldPosition(xform, xformQuery);

        _areaMarkers.Clear();
        lookup.GetEntitiesInRange(xform.MapID, worldPos, Range, _areaMarkers);

        foreach (var area in _areaMarkers)
        {
            if (area.Comp.Department == Department)
                return !Inverted;
        }

        return Inverted;
    }
}
