using Content.Pirate.Shared.Alchemy.Components;
using Content.Shared.Stunnable;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistMagnusElixirSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistMagnusElixirComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<AlchemistMagnusElixirComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(ent.Comp.CollapseKnockdownSeconds), true, false, true, true);
        _stun.TryAddParalyzeDuration(ent.Owner, TimeSpan.FromSeconds(ent.Comp.CollapseParalyzeSeconds));
    }
}
