using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared.Coordinates;
using Content.Shared.Flash;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This system enables a psionic being to emit a flash of psionic energy that blinds everyone nearby.
/// </summary>
public sealed class PsionicFlashPowerSystem : BasePsionicPowerSystem<PsionicFlashPowerComponent, PsionicFlashPowerActionEvent>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;

    protected override void OnPowerUsed(Entity<PsionicFlashPowerComponent> psionic, ref PsionicFlashPowerActionEvent args)
    {
        if (psionic.Comp.AbilitySound != null)
            _audio.PlayPredicted(psionic.Comp.AbilitySound, psionic, psionic);

        var duration = TimeSpan.FromSeconds(psionic.Comp.FlashDuration);
        _flash.FlashArea(psionic.Owner, args.Performer, psionic.Comp.Range, duration, psionic.Comp.SlowTo);

        SpawnAttachedTo(psionic.Comp.Effect, psionic.Owner.ToCoordinates());
        args.Handled = true;
        AfterPowerUsed(psionic, args.Performer);
    }
}
