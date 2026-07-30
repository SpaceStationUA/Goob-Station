// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Heretic.Lock;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Shitcode.Heretic.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Heretic.Lock;

/// <summary>
/// Applies Opening Blade's enhanced wounds without patching the upstream Heretic blade system.
/// </summary>
public sealed class OpeningBladeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHereticSystem _heretic = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteDamageGroup = "Brute";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpeningBladeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<OpeningBladeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            !_heretic.TryGetHereticComponent(args.User, out var heretic, out _) ||
            heretic is not { CurrentPath: "Lock", PathStage: >= 7 })
            return;

        var woundingMultiplier = heretic.Ascended ? 3f : 2f;
        foreach (var damageType in args.BaseDamage.DamageDict.Keys)
        {
            if (!args.BaseDamage.WoundSeverityMultipliers.TryAdd(damageType, woundingMultiplier))
                args.BaseDamage.WoundSeverityMultipliers[damageType] *= woundingMultiplier;
        }

        if (!TryComp(args.User, out TargetingComponent? targeting))
            return;

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);
        var woundProbability = heretic.Ascended ? 0.65f : 0.35f;

        foreach (var target in args.HitEntities)
        {
            var targetPart = _body.GetBodyChildrenOfType(target, partType, symmetry: symmetry).FirstOrNull();
            if (targetPart == null)
                continue;

            ApplyLockBladeEffect(target, targetPart.Value.Id, woundProbability);
        }
    }

    private void ApplyLockBladeEffect(EntityUid target, EntityUid targetPart, float probability)
    {
        if (!_random.Prob(probability) ||
            !_wounds.TryInduceWound(targetPart,
                "WeepingAvulsion",
                25f,
                out _,
                damageGroup: BruteDamageGroup))
            return;

        var effectAmount = _random.Next(3, 6);

        // A mangled chest is opened so the heart can be removed for the ascension ritual.
        if (TryComp(targetPart, out WoundableComponent? woundable) &&
            woundable.RootWoundable == targetPart &&
            woundable.WoundableSeverity >= WoundableSeverity.Mangled &&
            (!EnsureComp<SkinRetractedComponent>(targetPart, out _) |
             !EnsureComp<IncisionOpenComponent>(targetPart, out _) |
             !EnsureComp<BonesSawedComponent>(targetPart, out _) |
             !EnsureComp<BonesOpenComponent>(targetPart, out _)))
        {
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Pirate/Heretic/goresplat.ogg"),
                target,
                AudioParams.Default.WithVolume(10f));
            effectAmount *= 2;
        }
        else
        {
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Goobstation/Heretic/blood3.ogg"), target);
        }

        if (!TryComp(target, out BloodstreamComponent? bloodstream) ||
            !_solution.ResolveSolution(target,
                bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution,
                out var bloodSolution) ||
            bloodSolution.Volume < 3)
            return;

        var coordinates = _transform.GetMapCoordinates(target);
        for (var i = 0; i < effectAmount; i++)
        {
            var blood = bloodSolution.SplitSolution(3);
            var color = blood.GetColor(_prototype);
            var chunk = Spawn("BloodChunkEffect", coordinates);
            EnsureComp<BloodSplatterOnLandComponent>(chunk).Color = color;

            if (!_solution.TryGetSolution(chunk, "print", out var solutionEntity, true) ||
                !_solution.TryAddSolution(solutionEntity.Value, blood))
            {
                Del(chunk);
                break;
            }

            if (TryComp(chunk, out TrailComponent? trail))
            {
                trail.Color = color;
                Dirty(chunk, trail);
            }

            _throwing.TryThrow(chunk,
                direction: _random.NextAngle().ToVec() * _random.NextVector2(1f, 3f),
                baseThrowSpeed: _random.NextFloat(1f, 2.5f),
                pushbackRatio: 0f,
                friction: 2f,
                recoil: false,
                playSound: false);

            if (bloodSolution.Volume < 3)
                break;
        }
    }
}
