// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Targeting.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.Medical.LimbFixation;

public sealed class LimbFixationSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableComponent, BeforeTraumaInducedEvent>(OnBeforeTraumaInduced);
        SubscribeLocalEvent<WoundableComponent, WoundableIntegrityChangedEvent>(OnWoundableIntegrityChanged);
        SubscribeLocalEvent<LimbFixationComponent, BeforeTraumaticAmputationEvent>(OnBeforeTraumaticAmputation);
        SubscribeLocalEvent<LimbFixationDamageComponent, ComponentStartup>(OnDamageStartup);
        SubscribeLocalEvent<LimbFixationDamageComponent, ComponentShutdown>(OnDamageShutdown);
        SubscribeLocalEvent<LimbFixationComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<LimbFixationComponent, RejuvenateEvent>(OnRejuvenate, after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<LimbFixationComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        SubscribeLocalEvent<SurgeryRestoreLimbFunctionStepComponent, SurgeryStepEvent>(OnRestoreLimbFunction);
        SubscribeLocalEvent<SurgeryRestoreLimbFunctionStepComponent, SurgeryStepCompleteCheckEvent>(OnRestoreLimbFunctionCheck);
        SubscribeLocalEvent<SurgeryFunctionalPartConditionComponent, SurgeryValidEvent>(OnFunctionalPartCondition);
    }

    private void OnBeforeTraumaInduced(Entity<WoundableComponent> ent, ref BeforeTraumaInducedEvent args)
    {
        if (args.TraumaType != TraumaSystem.Dismemberment
            || !TryComp<BodyPartComponent>(ent, out var part)
            || part.Body is not { } body
            || part.PartType == BodyPartType.Chest
            || !HasComp<LimbFixationComponent>(body))
            return;

        args.Cancelled = true;
        EnsureComp<LimbFixationDamageComponent>(ent);
    }

    private void OnWoundableIntegrityChanged(
        Entity<WoundableComponent> ent,
        ref WoundableIntegrityChangedEvent args)
    {
        if (args.NewIntegrity > 0
            || !TryComp<BodyPartComponent>(ent, out var part)
            || part.Body is not { } body
            || part.PartType == BodyPartType.Chest
            || !HasComp<LimbFixationComponent>(body))
            return;

        EnsureComp<LimbFixationDamageComponent>(ent);
    }

    private void OnBeforeTraumaticAmputation(
        Entity<LimbFixationComponent> ent,
        ref BeforeTraumaticAmputationEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var part)
            || part.Body != ent.Owner
            || part.PartType == BodyPartType.Chest)
            return;

        args.Cancelled = true;
        EnsureComp<LimbFixationDamageComponent>(args.Part);
    }

    private void OnDamageStartup(Entity<LimbFixationDamageComponent> ent, ref ComponentStartup args)
    {
        RefreshForPart(ent);
    }

    private void OnDamageShutdown(Entity<LimbFixationDamageComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RefreshForPart(ent);
    }

    private void OnBodyPartAdded(Entity<LimbFixationComponent> ent, ref BodyPartAddedEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var bodyComp)
            || !_body.GetBodyChildren(ent, bodyComp).Any(part => HasActiveDamage(part.Id)))
            return;

        RefreshFunctionalState(ent, bodyComp);
    }

    private void OnRejuvenate(Entity<LimbFixationComponent> ent, ref RejuvenateEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var bodyComp))
            return;

        foreach (var part in _body.GetBodyChildren(ent, bodyComp).ToArray())
            RemComp<LimbFixationDamageComponent>(part.Id);
    }

    private void OnStandUpAttempt(Entity<LimbFixationComponent> ent, ref StandUpAttemptEvent args)
    {
        if (TryComp<BodyComponent>(ent, out var bodyComp)
            && bodyComp.RequiredLegs > 0
            && !HasEnabledLeg(bodyComp))
            args.Cancelled = true;
    }

    private void OnRestoreLimbFunction(
        Entity<SurgeryRestoreLimbFunctionStepComponent> ent,
        ref SurgeryStepEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var part)
            || part.Body != args.Body
            || !TryComp<WoundableComponent>(args.Part, out var woundable))
            return;

        if (!RestoreCriticalIntegrity((args.Part, woundable)))
            return;

        RemComp<LimbFixationDamageComponent>(args.Part);
    }

    private void OnRestoreLimbFunctionCheck(
        Entity<SurgeryRestoreLimbFunctionStepComponent> ent,
        ref SurgeryStepCompleteCheckEvent args)
    {
        if (HasActiveDamage(args.Part) || HasDisabledTargetingStatus(args.Body, args.Part))
            args.Cancelled = true;
    }

    private void OnFunctionalPartCondition(
        Entity<SurgeryFunctionalPartConditionComponent> ent,
        ref SurgeryValidEvent args)
    {
        args.Cancelled |= HasActiveDamage(args.Part)
            || HasComp<LimbFixationDisabledComponent>(args.Part);
    }

    private bool RestoreCriticalIntegrity(Entity<WoundableComponent> part)
    {
        if (!part.Comp.Thresholds.TryGetValue(WoundableSeverity.Critical, out var criticalIntegrity))
            return false;

        if (part.Comp.WoundableIntegrity >= criticalIntegrity)
            return true;

        var attempts = part.Comp.Wounds.Count + 1;
        while (part.Comp.WoundableIntegrity < criticalIntegrity && attempts-- > 0)
        {
            var previousIntegrity = part.Comp.WoundableIntegrity;
            _wounds.TryHealWoundsOnWoundable(
                part.Owner,
                criticalIntegrity - previousIntegrity,
                out _,
                part.Comp,
                ignoreMultipliers: true,
                ignoreBlockers: true);

            if (part.Comp.WoundableIntegrity <= previousIntegrity)
                break;
        }

        return part.Comp.WoundableIntegrity >= criticalIntegrity;
    }

    private bool HasDisabledTargetingStatus(EntityUid body, EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp)
            || !TryComp<TargetingComponent>(body, out var targeting))
            return false;

        return targeting.BodyStatus.TryGetValue(_body.GetTargetBodyPart(partComp), out var status)
            && status == WoundableSeverity.Disabled;
    }

    private void RefreshForPart(EntityUid uid)
    {
        if (!TryComp<BodyPartComponent>(uid, out var part) || part.Body is not { } body)
            return;

        RefreshFunctionalState(body, Comp<BodyComponent>(body));
    }

    private void RefreshFunctionalState(EntityUid body, BodyComponent bodyComp)
    {
        var changed = false;

        foreach (var (partId, part) in _body.GetBodyChildren(body, bodyComp).ToArray())
        {
            if (ShouldDisablePart(body, (partId, part)))
            {
                if (!part.Enabled)
                    continue;

                EnsureComp<LimbFixationDisabledComponent>(partId);
                SetPartEnabled(body, (partId, part), false);
                changed = true;
                continue;
            }

            if (!RemComp<LimbFixationDisabledComponent>(partId)
                || !CanEnablePart((partId, part))
                || part.Enabled)
                continue;

            SetPartEnabled(body, (partId, part), true);
            changed = true;
        }

        if (changed)
        {
            _body.UpdateMovementSpeed(body, bodyComp);

            if (bodyComp.RequiredLegs > 0 && !HasEnabledLeg(bodyComp))
                _standing.Down(body);
        }

        RefreshTargeting(body);
    }

    private bool ShouldDisablePart(EntityUid body, Entity<BodyPartComponent> part)
    {
        var current = part.Owner;
        while (true)
        {
            if (HasActiveDamage(current))
                return true;

            if (!_body.TryGetParentBodyPart(current, out var parent, out _) || parent is null)
                break;

            current = parent.Value;
        }

        if (part.Comp.PartType != BodyPartType.Leg)
            return false;

        return _body.GetBodyChildrenOfType(
                body,
                BodyPartType.Foot,
                symmetry: part.Comp.Symmetry)
            .Any(foot => HasActiveDamage(foot.Id));
    }

    private bool HasActiveDamage(EntityUid part)
    {
        return TryComp<LimbFixationDamageComponent>(part, out var damage)
            && damage.LifeStage < ComponentLifeStage.Stopping;
    }

    private bool CanEnablePart(Entity<BodyPartComponent> part)
    {
        if (!part.Comp.CanEnable)
            return false;

        var current = part.Owner;
        while (_body.TryGetParentBodyPart(current, out var parent, out var parentPart) && parent is not null)
        {
            if (parentPart is not { Enabled: true })
                return false;

            current = parent.Value;
        }

        return true;
    }

    private bool HasEnabledLeg(BodyComponent body)
    {
        return body.LegEntities.Any(leg =>
            TryComp<BodyPartComponent>(leg, out var part) && part.Enabled);
    }

    private void SetPartEnabled(EntityUid body, Entity<BodyPartComponent> part, bool enabled)
    {
        part.Comp.Enabled = enabled;
        Dirty(part);

        if (enabled)
        {
            var ev = new BodyPartEnabledEvent(part);
            RaiseLocalEvent(body, ref ev);
        }
        else
        {
            var ev = new BodyPartDisabledEvent(part);
            RaiseLocalEvent(body, ref ev);
        }
    }

    private void RefreshTargeting(EntityUid body)
    {
        if (!TryComp<TargetingComponent>(body, out var targeting))
            return;

        targeting.BodyStatus = _wounds.GetWoundableStatesOnBodyPainFeels(body);
        Dirty(body, targeting);

        if (_net.IsServer)
            RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(body)), body);
    }
}
