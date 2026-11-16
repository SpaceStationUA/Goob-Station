using Content.Shared._Pirate.Vampirism;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Pirate.Client.Vampire;

/// <summary>
/// Swaps the certificate sprite between its closed and opened states.
/// </summary>
public sealed class VampireCertificateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireCertificateComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VampireCertificateComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
    }

    private void OnComponentStartup(Entity<VampireCertificateComponent> ent, ref ComponentStartup args)
    {
        UpdateVisual(ent);
    }

    private void OnStateUpdated(Entity<VampireCertificateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisual(ent);
    }

    private void UpdateVisual(Entity<VampireCertificateComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var state = ent.Comp.IsClosed ? ent.Comp.ClosedState : ent.Comp.OpenState;
        if (sprite.LayerGetState(0).Name != state)
            sprite.LayerSetState(0, state);
    }
}
