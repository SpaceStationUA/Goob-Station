using Content.Client.Doors;
using Content.Client.IconSmoothing;
using Content.Client._Shitcode.Heretic;
using Content.Shared._Pirate.Doors;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Doors;

public sealed class SmoothedSecretDoorSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IconSmoothSystem _iconSmooth = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SmoothedSecretDoorComponent, AppearanceChangeEvent>(OnAppearanceChange, after: [typeof(DoorSystem)]);
        // IconSmooth recreates visible corner layers at startup.
        SubscribeLocalEvent<SmoothedSecretDoorComponent, IconSmoothCornersInitializedEvent>(OnCornersInitialized);
    }

    private void OnAppearanceChange(Entity<SmoothedSecretDoorComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        Update((ent.Owner, args.Sprite), args.Component);
    }

    private void OnCornersInitialized(Entity<SmoothedSecretDoorComponent> ent, ref IconSmoothCornersInitializedEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            Update((ent.Owner, sprite));
    }

    private void Update(Entity<SpriteComponent> ent, AppearanceComponent? appearance = null)
    {
        if (!_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state, appearance))
            state = DoorState.Closed;

        var closed = state == DoorState.Closed;
        _iconSmooth.SetCornersVisible(ent.AsNullable(), closed);

        if (_sprite.LayerMapTryGet(ent.AsNullable(), DoorVisualLayers.Base, out var layer, false))
            _sprite.LayerSetVisible(ent.AsNullable(), layer, !closed);
    }
}
