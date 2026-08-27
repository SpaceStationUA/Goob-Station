using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems; // Pirate: trauma bloodloss lifecycle
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems; // Pirate: trauma bloodloss lifecycle
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Rejuvenate; // Pirate: trauma bloodloss lifecycle
using Robust.Shared.Timing;

namespace Content.Server._Shitmed.Medical.Trauma;

public sealed class TraumaBloodlossSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!; // Pirate: trauma bloodloss lifecycle
    [Dependency] private readonly WoundSystem _wounds = default!; // Pirate: trauma bloodloss lifecycle

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraumaBloodlossComponent, TraumaInducedEvent>(OnInduced);
        SubscribeLocalEvent<TraumaBloodlossComponent, TraumaBeingRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<BloodstreamComponent, BodyPartAddedEvent>(OnBodyPartAdded); // Pirate: trauma bloodloss lifecycle
        SubscribeLocalEvent<BloodstreamComponent, BodyPartRemovedEvent>(OnBodyPartRemoved); // Pirate: trauma bloodloss lifecycle
        SubscribeLocalEvent<ConstantBleedComponent, RejuvenateEvent>(OnRejuvenate); // Pirate: trauma bloodloss lifecycle
    }

    #region Pirate: trauma bloodloss lifecycle

    private void OnBodyPartAdded(Entity<BloodstreamComponent> ent, ref BodyPartAddedEvent args)
    {
        AdjustConstantBleed(ent, GetTraumaBloodloss(args.Part));
    }

    private void OnBodyPartRemoved(Entity<BloodstreamComponent> ent, ref BodyPartRemovedEvent args)
    {
        AdjustConstantBleed(ent, -GetTraumaBloodloss(args.Part));
    }

    private void OnRejuvenate(Entity<ConstantBleedComponent> ent, ref RejuvenateEvent args)
    {
        RemComp<ConstantBleedComponent>(ent);
    }

    private float GetTraumaBloodloss(EntityUid rootPart)
    {
        var amount = 0f;

        foreach (var woundable in _wounds.GetAllWoundableChildren(rootPart))
        {
            if (!_trauma.TryGetWoundableTrauma(woundable, out var traumas, woundableComp: woundable.Comp))
                continue;

            foreach (var trauma in traumas)
            {
                if (TryComp<TraumaBloodlossComponent>(trauma, out var bloodloss))
                    amount += bloodloss.Amount;
            }
        }

        return amount;
    }

    private void AdjustConstantBleed(EntityUid body, float adjustment)
    {
        if (adjustment == 0f)
            return;

        if (adjustment > 0f)
        {
            EnsureComp<ConstantBleedComponent>(body).Amount += adjustment;
            return;
        }

        if (!TryComp<ConstantBleedComponent>(body, out var bleed))
            return;

        bleed.Amount += adjustment;
        if (bleed.Amount <= 0f)
            RemComp<ConstantBleedComponent>(body);
    }

    #endregion

    private void OnInduced(Entity<TraumaBloodlossComponent> ent, ref TraumaInducedEvent args)
    {
        if (!TryGetBody(args.Trauma.Comp.HoldingWoundable, out var body))
            return;

        AdjustConstantBleed(body, ent.Comp.Amount); // Pirate: trauma bloodloss lifecycle
    }

    private void OnRemoved(Entity<TraumaBloodlossComponent> ent, ref TraumaBeingRemovedEvent args)
    {
        if (!TryGetBody(args.Trauma.Comp.HoldingWoundable, out var body))
            return;

        AdjustConstantBleed(body, -ent.Comp.Amount); // Pirate: trauma bloodloss lifecycle
    }

    private bool TryGetBody(EntityUid? woundable, out EntityUid body)
    {
        body = default;
        if (woundable == null
            || !TryComp<BodyPartComponent>(woundable, out var part)
            || part.Body == null)
            return false;

        body = part.Body.Value;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<ConstantBleedComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var bleed, out var bloodstream))
        {
            var deficit = bleed.Amount - bloodstream.BleedAmountNotFromWounds;
            if (deficit > 0)
                _blood.TryModifyBleedAmount((uid, bloodstream), deficit);
        }
    }
}
