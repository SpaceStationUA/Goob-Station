// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Forging;

/// <summary>
/// Performs one bounded nearby-ingot query only when a player chooses an anvil recipe.
/// </summary>
public sealed class AnvilSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ForgingSystem _forging = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMetalSystem _metal = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<Entity<MetalIngotComponent>> _ingots = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ForgingAnvilComponent>(AnvilUiKey.Key, subscriptions =>
        {
            subscriptions.Event<AnvilStartItemMessage>(OnStartItem);
        });
    }

    private void OnStartItem(Entity<ForgingAnvilComponent> ent, ref AnvilStartItemMessage args)
        => TryStartItem(ent, args.Actor, args.Metal, args.Item);

    public EntityUid? TryStartItem(
        Entity<ForgingAnvilComponent> ent,
        EntityUid actor,
        ProtoId<MetalPrototype> metalId,
        ProtoId<ForgedItemPrototype> itemId)
    {
        if (!_prototypes.TryIndex(metalId, out var metal) ||
            !_prototypes.TryIndex(itemId, out var item) ||
            !_forging.CanMakeFrom(item, metalId))
            return null;

        var coordinates = FindIngots(ent, metalId);
        var cost = Math.Max(0, item.Cost * ent.Comp.CostScale);
        if (_ingots.Count < cost)
        {
            _popup.PopupEntity(Loc.GetString(
                    "forging-anvil-missing-ingots",
                    ("amount", cost - _ingots.Count),
                    ("metal", metal.Name)),
                ent.Owner,
                actor,
                PopupType.MediumCaution);
            return null;
        }

        var consumed = 0;
        if (cost > 0)
        {
            foreach (var ingot in _ingots)
            {
                PredictedDel(ingot.Owner);
                if (++consumed == cost)
                    break;
            }
        }

        var result = _forging.SpawnUnfinished(coordinates, metalId, itemId, ent.Comp.WorkScale);
        _popup.PopupEntity(Loc.GetString("forging-anvil-started", ("item", result)), ent.Owner, actor);
        _audio.PlayPredicted(ent.Comp.StartSound, ent.Owner, actor);
        _adminLog.Add(LogType.EntitySpawn, LogImpact.Low,
            $"{actor:player} created {result:item} on anvil {ent.Owner:used}");
        return result;
    }

    public EntityCoordinates FindIngots(
        Entity<ForgingAnvilComponent> ent,
        ProtoId<MetalPrototype> metal)
    {
        var coordinates = Transform(ent.Owner).Coordinates;
        _ingots.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.IngotRange, _ingots, LookupFlags.Uncontained);
        _ingots.RemoveWhere(ingot => !_metal.TryGetMetal(ingot.Owner, out var found) ||
                                    found != metal ||
                                    !_metal.IsWorkable(ingot.Owner));
        return coordinates;
    }
}
