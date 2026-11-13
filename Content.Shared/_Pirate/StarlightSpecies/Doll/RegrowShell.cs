using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.StarlightSpecies.Doll;

public sealed partial class RegrowShell : EntityEffectBase<RegrowShell>
{
    [DataField]
    public EntProtoId ShellProto = "ShellFragmentDoll";

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-regrow-doll-shell", ("chance", Probability));
}

public sealed partial class RegrowShellEntityEffectSystem : EntityEffectSystem<ShellComponent, RegrowShell>
{
    [Dependency] private readonly DollShellSystem _shell = default!;

    protected override void Effect(Entity<ShellComponent> entity, ref EntityEffectEvent<RegrowShell> args) =>
        _shell.Regrow(entity, args.Effect.ShellProto);
}
