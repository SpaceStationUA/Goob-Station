// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Movement.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.ZLevels.View;

/// <summary>
/// Lets a station AI use device UIs on a deck other than its core's.
/// </summary>
/// <remarks>
/// The AI brain stays in its core while its eye may view another deck map. The engine rejects cross-map
/// actors before range events run, so this system temporarily sets range to zero and checks actors against
/// the eye's map.
/// </remarks>
public sealed class ZStationAiDeckUiSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private EntityQuery<IgnoreUIRangeComponent> _ignoreRangeQuery;
    private EntityQuery<RelayInputMoverComponent> _relayQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<Enum> _keys = new();
    private readonly List<EntityUid> _actors = new();

    public override void Initialize()
    {
        base.Initialize();

        _ignoreRangeQuery = GetEntityQuery<IgnoreUIRangeComponent>();
        _relayQuery = GetEntityQuery<RelayInputMoverComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<StationAiWhitelistComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnUiOpened(Entity<StationAiWhitelistComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!IsCrossDeckViewer(args.Actor, ent.Owner) ||
            !TryComp<UserInterfaceComponent>(ent, out var ui) ||
            !_ui.TryGetInterfaceData((ent.Owner, ui), args.UiKey, out var data) ||
            data.InteractionRange <= 0f)
        {
            return;
        }

        // The client lifts it too, or a predicted open would close itself before the server state arrives.
        // Prediction rollback reverts it together with the open, so only the server keeps the original.
        if (_net.IsClient)
        {
            if (_timing.ApplyingState)
                return;
        }
        else
        {
            EnsureComp<ZStationAiDeckUiComponent>(ent).LiftedRanges[args.UiKey] = data.InteractionRange;
        }

        // A new instance: entries may be shared, and the old one must keep its range.
        _ui.SetUi((ent.Owner, ui), args.UiKey, new InterfaceData(data) { InteractionRange = 0f });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ZStationAiDeckUiComponent, UserInterfaceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lifted, out var ui, out var xform))
        {
            _keys.Clear();
            _keys.AddRange(lifted.LiftedRanges.Keys);

            foreach (var key in _keys)
            {
                var range = lifted.LiftedRanges[key];
                if (_ui.TryGetInterfaceData((uid, ui), key, out var data) &&
                    EnforceRange((uid, ui, xform), key, new InterfaceData(data) { InteractionRange = range }))
                {
                    continue;
                }

                lifted.LiftedRanges.Remove(key);
                if (data != null)
                    _ui.SetUi((uid, ui), key, new InterfaceData(data) { InteractionRange = range });
            }

            if (lifted.LiftedRanges.Count == 0)
                RemCompDeferred<ZStationAiDeckUiComponent>(uid);
        }
    }

    /// <summary>
    /// Closes the UI for every actor out of range, mirroring the engine's check.
    /// </summary>
    /// <returns>True while a station AI still uses it from another deck, so the range must stay lifted.</returns>
    private bool EnforceRange(Entity<UserInterfaceComponent, TransformComponent> ent, Enum key, InterfaceData data)
    {
        _actors.Clear();
        _actors.AddRange(_ui.GetActors((ent.Owner, ent.Comp1), key));

        var crossDeck = false;
        foreach (var actor in _actors)
        {
            if (!InRange(ent, key, data, actor))
            {
                _ui.CloseUi((ent.Owner, ent.Comp1), key, actor);
                continue;
            }

            crossDeck |= IsCrossDeckViewer(actor, ent.Owner);
        }

        return crossDeck;
    }

    private bool InRange(Entity<UserInterfaceComponent, TransformComponent> ent, Enum key, InterfaceData data, EntityUid actor)
    {
        if (!_xformQuery.TryComp(actor, out var actorXform) ||
            !_xformQuery.TryComp(GetViewer(actor), out var viewerXform) ||
            viewerXform.MapID != ent.Comp2.MapID)
        {
            return false;
        }

        var ev = new BoundUserInterfaceCheckRangeEvent((ent.Owner, ent.Comp2), key, data, (actor, actorXform));
        RaiseLocalEvent(ent.Owner, ref ev, true);

        if (ev.Result == BoundUserInterfaceRangeResult.Pass || _ignoreRangeQuery.HasComp(actor))
            return true;

        if (ev.Result == BoundUserInterfaceRangeResult.Fail)
            return false;

        return _xforms.InRange((ent.Owner, ent.Comp2), (actor, actorXform), data.InteractionRange);
    }

    /// <summary>
    /// The entity whose map an actor's UIs are judged by: a station AI's eye, else the actor itself.
    /// </summary>
    private EntityUid GetViewer(EntityUid actor)
    {
        return HasComp<StationAiOverlayComponent>(actor) && _relayQuery.TryComp(actor, out var relay)
            ? relay.RelayEntity
            : actor;
    }

    /// <summary>
    /// A station AI looking at <paramref name="target"/>'s deck from a core on another one.
    /// </summary>
    private bool IsCrossDeckViewer(EntityUid actor, EntityUid target)
    {
        var viewer = GetViewer(actor);
        if (viewer == actor ||
            !_xformQuery.TryComp(actor, out var actorXform) ||
            !_xformQuery.TryComp(viewer, out var viewerXform) ||
            !_xformQuery.TryComp(target, out var targetXform))
        {
            return false;
        }

        return actorXform.MapID != targetXform.MapID && viewerXform.MapID == targetXform.MapID;
    }
}
