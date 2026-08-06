using Content.Pirate.Shared.Blinking;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Examine;

namespace Content.Server._DV.Psionics.Systems;

/// <summary>
///     Handles the side effects of the permanent MindBroken state: stripping psionics and
///     psionic potential, granting full psionic insulation, adding the Assay response and
///     the close-examine description.
/// </summary>
public sealed class MindBrokenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindBrokenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MindBrokenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MindBrokenComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(Entity<MindBrokenComponent> ent, ref ComponentStartup args)
    {
        // Grab any examine-description override from the (about to be removed) psionic potential.
        if (ent.Comp.MindbrokenExamineDesc is null
            && TryComp<PotentialPsionicComponent>(ent, out var potential)
            && potential.MindbrokenExamineDesc is { } desc)
        {
            ent.Comp.MindbrokenExamineDesc = desc;
            Dirty(ent);
        }

        // Mindbroken entities are neither psionics nor potential psionics.
        RemComp<PsionicComponent>(ent);
        RemComp<PotentialPsionicComponent>(ent);

        // Full psionic insulation: cannot use, cannot be targeted.
        var insulative = EnsureComp<PsionicallyInsulativeComponent>(ent);
        insulative.AllowsPsionicUsage = false;
        insulative.ShieldsFromPsionics = true;
        Dirty(ent, insulative);

        // Assaying a mindbroken entity reports the blessing that once lived in it.
        var assay = EnsureComp<AssayResponseComponent>(ent);
        assay.ReplaceMessage ??= "mindbroken-assay";
        Dirty(ent, assay);

        // Mindbroken entities never blink.
        if (TryComp<BlinkingComponent>(ent, out var blinking) && blinking.Enabled)
        {
            blinking.Enabled = false;
            Dirty(ent, blinking);
            ent.Comp.BlinkingDisabled = true;
        }

        var ev = new MindBrokenAddedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnShutdown(Entity<MindBrokenComponent> ent, ref ComponentShutdown args)
    {
        // Undo the granted insulation & assay response when the mindbroken state is removed.
        RemComp<PsionicallyInsulativeComponent>(ent);
        RemComp<AssayResponseComponent>(ent);

        // Restore blinking if this mindbroken state disabled it.
        if (ent.Comp.BlinkingDisabled && TryComp<BlinkingComponent>(ent, out var blinking))
        {
            blinking.Enabled = true;
            Dirty(ent, blinking);
        }
    }

    private void OnExamined(Entity<MindBrokenComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var desc = ent.Comp.MindbrokenExamineDesc ?? "mindbroken-examine";
        args.PushMarkup(Loc.GetString(desc, ("entity", ent.Owner)));
    }
}
