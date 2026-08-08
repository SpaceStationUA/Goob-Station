// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Heretic.Ritual;
using Content.Server.Polymorph.Systems;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Prototypes;
using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared.Tag;
using Robust.Server.Containers;

namespace Content.Pirate.Server.Heretic.Lock;

// Pirate: the source Shattered ritual uses effect-chain primitives that this branch does not have.
public sealed partial class RitualShatteredBehavior : RitualSacrificeBehavior
{
    private EntityUid? _target;
    private EntityUid? _gloves;
    private EntityUid? _armor;

    public override bool Execute(RitualData args, out string? outstr)
    {
        _target = null;
        _gloves = null;
        _armor = null;

        if (!base.Execute(args, out outstr))
            return false;

        // The source ritual excludes existing ghouls and accepts one corpse only.
        uids = uids
            .Where(uid => !args.EntityManager.HasComponent<GhoulComponent>(uid))
            .Take(1)
            .ToList();

        if (uids.Count == 0)
        {
            outstr = Loc.GetString("heretic-ritual-fail-ghoulify");
            return false;
        }

        _target = uids[0];

        var lookup = args.EntityManager.System<EntityLookupSystem>();
        var containers = args.EntityManager.System<ContainerSystem>();
        var tags = args.EntityManager.System<TagSystem>();

        foreach (var entity in lookup.GetEntitiesInRange(args.Platform, 1.5f))
        {
            if (containers.IsEntityInContainer(entity))
                continue;

            if (_gloves == null && (tags.HasTag(entity, "GlovesLatex") || tags.HasTag(entity, "GlovesNitrile")))
                _gloves = entity;

            if (_armor == null && args.EntityManager.HasComponent<ClothingComponent>(entity) &&
                args.EntityManager.HasComponent<ArmorComponent>(entity) &&
                args.EntityManager.HasComponent<AllowSuitStorageComponent>(entity) &&
                args.EntityManager.HasComponent<StaminaResistanceComponent>(entity))
                _armor = entity;
        }

        if (_gloves == null || _armor == null)
        {
            var missing = new List<string>();
            if (_gloves == null)
                missing.Add(Loc.GetString("heretic-ritual-ingredient-gloves-medical"));
            if (_armor == null)
                missing.Add(Loc.GetString("heretic-ritual-ingredient-armor-outer"));

            outstr = Loc.GetString("heretic-ritual-fail-items", ("itemlist", string.Join(", ", missing)));
            return false;
        }

        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        if (_gloves is { } gloves)
            args.EntityManager.QueueDeleteEntity(gloves);

        if (_armor is { } armor)
            args.EntityManager.QueueDeleteEntity(armor);

        if (_target is not { } target || !args.EntityManager.EntityExists(target))
        {
            Reset();
            return;
        }

        var risen = args.EntityManager.System<PolymorphSystem>().PolymorphEntity(target, "ShatteredRisen");
        if (risen is not { } risenEntity)
        {
            Reset();
            return;
        }

        var minion = args.EntityManager.EnsureComponent<HereticMinionComponent>(risenEntity);
        minion.BoundHeretic = args.Performer;

        args.EntityManager.AddComponent(risenEntity, new GhoulComponent
        {
            GiveBlade = false,
            TotalHealth = 250,
            ChangeAppearance = false,
            DropOrgansOnDeath = true,
        });

        args.Limited?.Add(risenEntity);
        Reset();
    }

    private void Reset()
    {
        _target = null;
        _gloves = null;
        _armor = null;
        uids.Clear();
    }
}
