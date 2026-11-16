using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._EinsteinEngines.Language.Components;
using Robust.Shared.Player;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// Xenoglossy is a passive psionic power that grants understanding of all languages
/// and the ability to speak the Psychomantic language, via <see cref="UniversalLanguageSpeakerComponent"/>.
/// No action button is added — the effect is always active while the power is held.
/// </summary>
public sealed class XenoglossyPowerSystem : BasePsionicPowerSystem<XenoglossyPowerComponent, XenoglossyPowerActionEvent>
{
    protected override void OnPowerInit(Entity<XenoglossyPowerComponent> power, ref MapInitEvent args)
    {
        // Replicate base OnPowerInit without the Action.AddAction / PsionicPowersActionEntities parts.

        // Pirate: removable psionic powers require psionic potential to persist on the entity.
        if (power.Comp.CanBeRemoved && !HasComp<PotentialPsionicComponent>(power))
        {
            RemComp<XenoglossyPowerComponent>(power);
            return;
        }

        var psionicComp = EnsureComp<PsionicComponent>(power);
        Dirty(power);

        // Merge any powers this power unlocks into the psionic's random pull
        if (power.Comp.PowerPoolAdditions.Count > 0)
        {
            foreach (var (protoId, weight) in power.Comp.PowerPoolAdditions)
                psionicComp.PowerPoolAdditions[protoId] = weight;

            Dirty(power, psionicComp);
        }

        // Feedback shown to the player when they first gain this power.
        if (power.Comp.PowerInitFeedback is { } feedback
            && TryComp<ActorComponent>(power, out _))
        {
            RaiseLocalEvent(power, new PsionicPowerGainedEvent(power, Loc.GetString(feedback)), broadcast: true);
        }

        // Grant understanding of all languages + Psychomantic speech.
        // UniversalLanguageSpeakerComponent is NOT networked, so we must not Dirty it.
        // The server-side LanguageSystem subscribes to its MapInitEvent and updates
        // the (networked) LanguageSpeakerComponent automatically.
        EnsureComp<LanguageSpeakerComponent>(power);
        EnsureComp<UniversalLanguageSpeakerComponent>(power);
    }

    // Never called — no action is added, so the event is never raised.
    protected override void OnPowerUsed(Entity<XenoglossyPowerComponent> psionic, ref XenoglossyPowerActionEvent args)
    {
    }

    protected override void OnMindBroken(Entity<XenoglossyPowerComponent> psionic, ref PsionicMindBrokenEvent args)
    {
        base.OnMindBroken(psionic, ref args);

        if (args.Success)
            RemComp<UniversalLanguageSpeakerComponent>(psionic);
    }

    protected override void OnPsionicallySuppressed(Entity<XenoglossyPowerComponent> power, ref PsionicSuppressedEvent args)
    {
        base.OnPsionicallySuppressed(power, ref args);

        if (Timing.ApplyingState)
            return;

        if (TryComp<UniversalLanguageSpeakerComponent>(power, out var ulsc))
            ulsc.Enabled = false;
    }

    protected override void OnStoppedPsionicallySuppressed(Entity<XenoglossyPowerComponent> psionic, ref PsionicStoppedSuppressedEvent args)
    {
        if (TryComp<UniversalLanguageSpeakerComponent>(psionic, out var ulsc))
            ulsc.Enabled = true;
    }
}
