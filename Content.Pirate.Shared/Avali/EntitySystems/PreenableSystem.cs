// SPDX-FileCopyrightText: 2026 kotobdev <59124164+kotobdev@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Avali.Components;
using Content.Pirate.Shared.Avali.Events;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Forensics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffect;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Avali.EntitySystems;

public sealed class PreenableSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedForensicsSystem _forensics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreenableComponent, GetVerbsEvent<Verb>>(AddVerb);
        SubscribeLocalEvent<PreenableComponent, PreeningEvent>(OnPreened);
        SubscribeLocalEvent<PreenableComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<PreenableComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<PreenableComponent, ComponentInit>(OnCompInit);
    }

    private void OnCompInit(Entity<PreenableComponent> ent, ref ComponentInit args)
    {
        ent.Comp.CurrentFeathers = ent.Comp.MaximumFeathers;
        Dirty(ent);
    }

    private void AddVerb(Entity<PreenableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess || ent.Comp.CurrentFeathers <= 0)
            return;

        var user = args.User;

        args.Verbs.Add(new Verb
        {
            Act = () => AttemptDoAfter(ent, user),
            Text = Loc.GetString(ent.Comp.PreeningVerbString),
        });
    }

    private void AttemptDoAfter(Entity<PreenableComponent> ent, EntityUid userUid)
    {
        var doArgs = new DoAfterArgs(EntityManager, userUid, 5f, new PreeningEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (userUid == ent.Owner)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.SelfPreeningMessage), ent, ent);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString(ent.Comp.GettingPreenedMessage, ("preener", Identity.Entity(userUid, EntityManager))),
                ent,
                ent,
                PopupType.Medium);
            _popup.PopupClient(
                Loc.GetString(ent.Comp.PreeningOtherMessage, ("preenee", Identity.Entity(ent, EntityManager))),
                userUid,
                userUid);
        }

        _doAfter.TryStartDoAfter(doArgs);
    }

    private void OnPreened(Entity<PreenableComponent> ent, ref PreeningEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.CurrentFeathers <= 0)
            return;

        args.Handled = true;
        var feather = SpawnFeather(ent, false);
        _hands.TryPickupAnyHand(args.User, feather);
    }

    private void OnDamaged(Entity<PreenableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || ent.Comp.ValidDamageGroups == null || !args.DamageIncreased)
            return;

        if (ent.Comp.CurrentFeathers <= 0)
            return;

        var totalApplicableDamage = FixedPoint2.Zero;
        foreach (var (group, value) in args.DamageDelta.GetDamagePerGroup(_prototype))
        {
            if (ent.Comp.ValidDamageGroups.Contains(group))
                totalApplicableDamage += value;
        }

        if (totalApplicableDamage <= ent.Comp.ShedDamageThreshold)
            return;

        // Pirate: keep DeltaV's predicted roll local instead of patching shared random helpers.
        var seed = SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id, 0);
        var random = new System.Random(seed);
        var triggerChance = totalApplicableDamage * ent.Comp.ShedScalingChance;

        if (random.NextDouble() >= triggerChance)
            return;

        var feather = SpawnFeather(ent, true);
        var scatterVector = random.NextAngle().ToVec() * random.NextFloat(10, 40);
        _physics.ApplyLinearImpulse(feather, scatterVector);
        _physics.ApplyAngularImpulse(feather, random.NextFloat(-30, 30));

        var meta = MetaData(feather);
        _metaData.SetEntityName(
            feather,
            Loc.GetString(ent.Comp.FeatherBloodiedNameString, ("item", Name(feather))),
            meta);
        _metaData.SetEntityDescription(feather, Loc.GetString(ent.Comp.FeatherBloodiedDescString), meta);
        Dirty(feather, meta);

        _popup.PopupClient(Loc.GetString(ent.Comp.DroppedFeatherString), ent, ent, PopupType.MediumCaution);
        _chat.TryEmoteWithoutChat(ent, ent.Comp.ScreamEmote);

        // Pirate: the legacy key alone only displays the alert, so include its intended slow-immunity component.
#pragma warning disable CS0618
        _statusEffects.TryAddStatusEffect<IgnoreSlowOnDamageComponent>(
            ent,
            "Adrenaline",
            TimeSpan.FromSeconds(3),
            true);
#pragma warning restore CS0618
    }

    private void OnDamageModify(Entity<PreenableComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.VulnerabilityModifier == null)
            return;

        var vulnerabilityModifier = 1f - ent.Comp.CurrentFeathers / (float) ent.Comp.MaximumFeathers;
        var damageSpecifier = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>(ent.Comp.VulnerabilityModifier.Coefficients),
        };

        foreach (var key in damageSpecifier.Coefficients.Keys)
        {
            damageSpecifier.Coefficients[key] =
                1f + (damageSpecifier.Coefficients[key] - 1f) * vulnerabilityModifier;
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, damageSpecifier);
    }

    private EntityUid SpawnFeather(Entity<PreenableComponent> ent, bool bloody)
    {
        var feather = PredictedSpawnAtPosition(ent.Comp.FeatherPrototype.Id, Transform(ent).Coordinates);

        if (TryComp<HumanoidAppearanceComponent>(ent, out var appearance))
            _appearance.SetData(feather, FeatherVisuals.FeatherColor, appearance.SkinColor);

        _forensics.TransferDna(feather, ent, false);

        ent.Comp.CurrentFeathers -= 1;
        ent.Comp.ReplenishTime = _timing.CurTime + ent.Comp.ReplenishDelay;
        Dirty(ent);

        if (!bloody || !TryComp<BloodstreamComponent>(ent, out var bloodstream) || bloodstream.BloodSolution == null)
            return feather;

        var solution = bloodstream.BloodSolution.Value.Comp.Solution;
        _appearance.SetData(feather, FeatherVisuals.BloodColor, solution.GetColor(_prototype));
        return feather;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        var query = EntityQueryEnumerator<PreenableComponent>();
        while (query.MoveNext(out var uid, out var preenable))
        {
            if (preenable.ReplenishTime == null || _timing.CurTime < preenable.ReplenishTime)
                continue;

            if (preenable.CurrentFeathers >= preenable.MaximumFeathers)
                continue;

            preenable.CurrentFeathers += 1;

            if (preenable.CurrentFeathers >= preenable.MaximumFeathers)
                preenable.ReplenishTime = null;
            else
                preenable.ReplenishTime = _timing.CurTime + preenable.ReplenishDelay;

            Dirty(uid, preenable);
        }
    }
}
