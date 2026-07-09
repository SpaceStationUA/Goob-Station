using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Overlays.Shockwave;

/// <summary>
/// Shared system that starts shockwave overlay timing.
/// </summary>
public abstract partial class SharedShockwaveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShockwaveComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ShockwaveComponent> entity, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        entity.Comp.StartTime = _timing.CurTime;
        Dirty(entity, entity.Comp);
    }
}
