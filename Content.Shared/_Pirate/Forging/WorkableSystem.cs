// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Forging;

public sealed class WorkableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMetalSystem _metal = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<WorkableComponent> _workableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _workableQuery = GetEntityQuery<WorkableComponent>();
        SubscribeLocalEvent<WorkableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<WorkableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDamageChanged(Entity<WorkableComponent> ent, ref DamageChangedEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner) || !_timing.IsFirstTimePredicted || ent.Comp.Amount <= 0 ||
            args.Origin is not { } user || args.DamageDelta is not { } delta ||
            !delta.DamageDict.TryGetValue(ent.Comp.DamageType, out var dealt) || dealt <= FixedPoint2.Zero)
            return;

        if (!_metal.IsWorkable(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("workable-metal-popup-too-cold"), ent.Owner, user);
            return;
        }

        ent.Comp.Remaining -= dealt;
        if (ent.Comp.Remaining <= FixedPoint2.Zero)
            CreateResult(ent, user);
        else
            DirtyField(ent, ent.Comp, nameof(WorkableComponent.Remaining));
    }

    private void OnExamined(Entity<WorkableComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("workable-metal-examine", ("workable", _metal.IsWorkable(ent.Owner))));
    }

    private void CreateResult(Entity<WorkableComponent> ent, EntityUid? user)
    {
        var transform = Transform(ent.Owner);
        var amount = ent.Comp.Amount;
        ent.Comp.Amount = 0;
        DirtyField(ent, ent.Comp, nameof(WorkableComponent.Amount));

        for (var i = 0; i < amount; i++)
        {
            var result = PredictedSpawnAtPosition(ent.Comp.Result, transform.Coordinates);
            _transform.SetLocalRotation(result, transform.LocalRotation);
            var ev = new MetalWroughtEvent(result, user);
            RaiseLocalEvent(ent.Owner, ref ev);
        }

        PredictedQueueDel(ent.Owner);
    }

    public void SetRemaining(Entity<WorkableComponent?> ent, FixedPoint2 value)
    {
        if (!_workableQuery.Resolve(ent, ref ent.Comp) || ent.Comp.Remaining == value)
            return;

        ent.Comp.Remaining = value;
        DirtyField(ent.Owner, ent.Comp, nameof(WorkableComponent.Remaining));
    }

    public void SetResult(Entity<WorkableComponent?> ent, EntProtoId result)
    {
        if (!_workableQuery.Resolve(ent, ref ent.Comp) || ent.Comp.Result == result)
            return;

        ent.Comp.Result = result;
        DirtyField(ent.Owner, ent.Comp, nameof(WorkableComponent.Result));
    }

    public void SetAmount(Entity<WorkableComponent?> ent, int amount)
    {
        if (!_workableQuery.Resolve(ent, ref ent.Comp) || ent.Comp.Amount == amount)
            return;

        ent.Comp.Amount = Math.Max(0, amount);
        DirtyField(ent.Owner, ent.Comp, nameof(WorkableComponent.Amount));
    }
}
