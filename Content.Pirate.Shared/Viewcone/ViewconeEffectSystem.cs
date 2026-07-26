// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Pirate.Common.CCVar;
using Content.Pirate.Common.Movement;
using Content.Pirate.Shared.Viewcone.Components;
using Content.Shared.Chat;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Analyzers;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Pirate.Shared.Viewcone;

/// <summary>
/// Handles footsteps creating out-of-vision effects.
/// Provides API for spawning viewcone effects and making sure source
/// gets set correctly + it spawns in the correct pos and shit
/// </summary>
public sealed partial class ViewconeEffectSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public static readonly EntProtoId TalkEffect = "ViewconeEffectTalk";

    private bool _disabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ViewconeFootstepsEffectComponent, FootStepEvent>(OnFootStep);
        SubscribeLocalEvent<ViewconeMeleeEffectComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);

        Subs.CVar(_cfg, PirateCVars.DisableVisionEffects, x => _disabled = x, true);
    }

    private void OnFootStep(Entity<ViewconeFootstepsEffectComponent> ent, ref FootStepEvent args)
    {
        // Silent shoes suppress the viewcone footstep effect
        var ev = new CanSpawnFootstepsEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
        if (ev.Cancelled)
            return;

        SpawnEffect(ent, ent.Comp.Effect, args.WorldAngle);
    }

    private void OnMeleeAttack(Entity<ViewconeMeleeEffectComponent> ent, ref MeleeAttackEvent args)
    {
        SpawnEffect(ent, ent.Comp.Effect);
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        // whispering is too quiet to get a fix on
        if (!args.IsWhisper)
            SpawnEffect(args.Source, TalkEffect);
    }

    /// <summary>
    /// Spawns the given effect entity at the player source, and sets relevant variables
    /// </summary>
    /// <param name="source">The player that originated the effect, or the entity to spawn next to if a relevant player doesn't exist</param>
    /// <param name="effect">The prototype ID of an effect entity to spawn (see viewcone_effects.yml)</param>
    /// <param name="angleOverride">The local rotation to set the effect to, instead of the parent rotation.</param>
    public void SpawnEffect(EntityUid source, [ForbidLiteral] EntProtoId effect, Angle? angleOverride = null)
    {
        // Pirate: the source also gated on IsFirstTimePredicted, but in this engine prediction resets
        // delete predicted spawns expecting them to be re-created on re-prediction, so the gate would
        // make the effect vanish after one tick.
        if (_disabled)
            return;

        var ent = PredictedSpawnNextToOrDrop(effect, source);
        var viewconeEffect = EnsureComp<ViewconeOccludableComponent>(ent);
        viewconeEffect.Inverted = true; // it's always visible
        viewconeEffect.Source = source;
        Dirty(ent, viewconeEffect);

        // set rotation
        // Pirate fix: angleOverride comes from FootStepEvent as a world-space angle, so apply it
        // in world space or footstep effects face the wrong way on rotated grids.
        if (angleOverride is { } worldAngle)
            _xform.SetWorldRotation(ent, worldAngle);
        else
            _xform.SetLocalRotation(ent, Transform(source).LocalRotation);

        // also ensure this in case somehow something without it gets here.
        EnsureComp<TimedDespawnComponent>(ent);
    }
}
