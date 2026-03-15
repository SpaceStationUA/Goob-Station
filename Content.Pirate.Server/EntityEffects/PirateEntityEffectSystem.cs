using Content.Pirate.Shared.Vampire;
using Content.Pirate.Shared.Vampire.Components;
using Content.Server.Abilities.Psionics;
using Content.Server.Psionics;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Chemistry.ReactionEffects;
using Content.Shared.Chemistry.ReagentEffects;
using Content.Shared.EntityEffects;
using Content.Shared.Psionics.Glimmer;

namespace Content.Pirate.Server.EntityEffects;

/// <summary>
/// Server-side execution for Pirate/EinsteinEngines custom <see cref="EventEntityEffect{T}"/> implementations.
/// </summary>
public sealed class PirateEntityEffectSystem : EntitySystem
{
    [Dependency] private readonly GlimmerSystem _glimmer = default!;
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilities = default!;
    [Dependency] private readonly PsionicsSystem _psionics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChangeGlimmerReactionEffect>>(OnExecuteChangeGlimmer);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChemRemovePsionic>>(OnExecuteChemRemovePsionic);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChemRerollPsionic>>(OnExecuteChemRerollPsionic);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<ChemRestorePsionicReroll>>(OnExecuteChemRestorePsionicReroll);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<CureVampire>>(OnExecuteCureVampire);
    }

    private void OnExecuteChangeGlimmer(ref ExecuteEntityEffectEvent<ChangeGlimmerReactionEffect> args)
    {
        // Only meaningful for reagent reactions.
        if (args.Args is not EntityEffectReagentArgs)
            return;

        _glimmer.DeltaGlimmerInput(args.Effect.Count);
    }

    private void OnExecuteChemRemovePsionic(ref ExecuteEntityEffectEvent<ChemRemovePsionic> args)
    {
        if (args.Args is not EntityEffectReagentArgs reagentArgs)
            return;

        if (reagentArgs.Scale != 1f)
            return;

        _psionicAbilities.MindBreak(reagentArgs.TargetEntity);
    }

    private void OnExecuteChemRerollPsionic(ref ExecuteEntityEffectEvent<ChemRerollPsionic> args)
    {
        if (args.Args is not EntityEffectReagentArgs)
            return;

        _psionics.RerollPsionics(args.Args.TargetEntity, bonusMuliplier: args.Effect.BonusMuliplier);
    }

    private void OnExecuteChemRestorePsionicReroll(ref ExecuteEntityEffectEvent<ChemRestorePsionicReroll> args)
    {
        if (args.Args is not EntityEffectReagentArgs)
            return;

        if (!TryComp(args.Args.TargetEntity, out PsionicComponent? psionicComp))
            return;

        if (!psionicComp.Roller && !args.Effect.BypassRoller)
            return;

        psionicComp.CanReroll = true;
        Dirty(args.Args.TargetEntity, psionicComp);
    }

    private void OnExecuteCureVampire(ref ExecuteEntityEffectEvent<CureVampire> args)
    {
        if (args.Args is not EntityEffectReagentArgs)
            return;

        // Only mark full antag vampires, not simple vampirism trait holders.
        if (!HasComp<VampireComponent>(args.Args.TargetEntity))
            return;

        EnsureComp<VampireCureComponent>(args.Args.TargetEntity);
    }
}

