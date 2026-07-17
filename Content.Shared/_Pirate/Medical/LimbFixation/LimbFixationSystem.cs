// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
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
        SubscribeLocalEvent<BodyPartComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate, after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<BodyComponent, StandUpAttemptEvent>(OnStandUpAttempt);
    }

    private void OnBeforeTraumaInduced(Entity<WoundableComponent> ent, ref BeforeTraumaInducedEvent args)
    {
        if (args.TraumaType != TraumaType.Dismemberment
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
        RefreshForPart(ent, null);
    }

    private void OnDamageShutdown(Entity<LimbFixationDamageComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RefreshForPart(ent, ent.Owner);
    }

    private void OnBodyPartAdded(Entity<BodyPartComponent> ent, ref BodyPartAddedEvent args)
    {
        if (ent.Comp.Body is not { } body
            || !TryComp<BodyComponent>(body, out var bodyComp)
            || !_body.GetBodyChildren(body, bodyComp).Any(part => HasComp<LimbFixationDamageComponent>(part.Id)))
            return;

        RefreshFunctionalState(body, bodyComp, null);
    }

    private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        foreach (var part in _body.GetBodyChildren(ent, ent.Comp).ToArray())
            RemComp<LimbFixationDamageComponent>(part.Id);
    }

    private void OnStandUpAttempt(Entity<BodyComponent> ent, ref StandUpAttemptEvent args)
    {
        if (ent.Comp.RequiredLegs > 0 && !HasEnabledLeg(ent.Comp))
            args.Cancelled = true;
    }

    private void RefreshForPart(EntityUid uid, EntityUid? ignoredDamage)
    {
        if (!TryComp<BodyPartComponent>(uid, out var part) || part.Body is not { } body)
            return;

        RefreshFunctionalState(body, Comp<BodyComponent>(body), ignoredDamage);
    }

    private void RefreshFunctionalState(EntityUid body, BodyComponent bodyComp, EntityUid? ignoredDamage)
    {
        var changed = false;

        foreach (var (partId, part) in _body.GetBodyChildren(body, bodyComp).ToArray())
        {
            if (ShouldDisablePart(body, (partId, part), ignoredDamage))
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

    private bool ShouldDisablePart(
        EntityUid body,
        Entity<BodyPartComponent> part,
        EntityUid? ignoredDamage)
    {
        var current = part.Owner;
        while (true)
        {
            if (HasActiveDamage(current, ignoredDamage))
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
            .Any(foot => HasActiveDamage(foot.Id, ignoredDamage));
    }

    private bool HasActiveDamage(EntityUid part, EntityUid? ignoredDamage)
    {
        return part != ignoredDamage && HasComp<LimbFixationDamageComponent>(part);
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
