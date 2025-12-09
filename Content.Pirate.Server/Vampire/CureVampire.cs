using Content.Pirate.Shared.Vampire.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Vampire;

/// <summary>
/// Holy water reagent effect that cures antag vampires back into mortals.
/// The actual cleanup is performed server-side when <see cref="VampireCureComponent"/>
/// is initialized on the target.
/// </summary>
public sealed partial class CureVampire : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        // No dedicated guidebook entry for now.
        return null;
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entMan = args.EntityManager;
        var uid = args.TargetEntity;

        // Only mark full antag vampires, not simple vampirism trait holders.
        if (!entMan.HasComponent<VampireComponent>(uid))
            return;

        entMan.EnsureComponent<VampireCureComponent>(uid);
    }
}