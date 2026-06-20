using Content.Shared._DV.Projectiles;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared.Coordinates;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This system enables a psionic being to break lights around them.
/// </summary>
public sealed class PsychokineticScreamPowerSystem : BasePsionicPowerSystem<PsychokineticScreamPowerComponent, PsychokineticScreamPowerActionEvent>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    protected override void OnPowerUsed(Entity<PsychokineticScreamPowerComponent> psionic, ref PsychokineticScreamPowerActionEvent args)
    {
        if (psionic.Comp.AbilitySound != null)
            _audio.PlayPredicted(psionic.Comp.AbilitySound, psionic, psionic);

        ShatterLightsAround(psionic.Owner, psionic.Comp.Radius, psionic.Comp.LineOfSight, psionic.Comp.PenetratingRadius);

        SpawnAttachedTo(psionic.Comp.Effect, psionic.Owner.ToCoordinates());
        args.Handled = true;
        AfterPowerUsed(psionic, args.Performer);
    }

    /// <summary>
    /// Shatter lights around an entity.
    /// This is public because other systems require it.
    /// </summary>
    /// <param name="source">The entity that caused it.</param>
    /// <param name="range">The range where the lights are broken within.</param>
    /// <param name="lineOfSight">Whether line of sight is required.</param>
    /// <param name="penetratingRadius">How far it ignores the line of sight.</param>
    [PublicAPI]
    public void ShatterLightsAround(EntityUid source, float range, bool lineOfSight, float penetratingRadius = 0f)
    {
        var ev = new PsychokineticScreamShatterLightEvent(source, range, lineOfSight, penetratingRadius);
        RaiseLocalEvent(ref ev);

        // Gets all all flare gun pellets in a radius and deletes them.
        HashSet<Entity<FlareGunPelletComponent>> flaresInRange = [];
        _lookup.GetEntitiesInRange(Transform(source).Coordinates, range, flaresInRange);

        foreach (var flare in flaresInRange)
        {
            PredictedQueueDel(flare);
        }
    }
}
