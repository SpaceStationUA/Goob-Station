using Content.Shared._Pirate.Origin.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Pirate.Client.Origin.Systems;

public sealed class PassportSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassportComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<PassportComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
    }

    private void OnComponentStartup(Entity<PassportComponent> passport, ref ComponentStartup args)
    {
        UpdateVisual(passport);
    }

    private void OnStateUpdated(Entity<PassportComponent> passport, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisual(passport);
    }

    private bool UpdateVisual(Entity<PassportComponent> passport)
    {
        if (!TryComp<SpriteComponent>(passport, out var sprite))
            return false;

        var currentState = sprite.LayerGetState(0).Name;
        if (currentState == null)
            return false;

        const string openSuffix = "_open";
        const string closedSuffix = "_closed";
        var suffix = currentState.EndsWith(openSuffix, StringComparison.Ordinal)
            ? openSuffix
            : currentState.EndsWith(closedSuffix, StringComparison.Ordinal)
                ? closedSuffix
                : null;

        if (suffix == null)
            return false;

        var targetState = currentState[..^suffix.Length] + (passport.Comp.IsClosed ? closedSuffix : openSuffix);
        if (targetState != currentState)
            sprite.LayerSetState(0, targetState);

        return true;
    }
}
