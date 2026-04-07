using Content.Client.Doors;
using Content.Shared._Pirate.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Power;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Doors;

public sealed class AirlockIlluminationSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockIlluminationComponent, AppearanceChangeEvent>(OnAppearanceChange, after: new[] { typeof(AirlockSystem) });
    }

    private void OnAppearanceChange(EntityUid uid, AirlockIlluminationComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<AirlockComponent>(uid, out var airlock))
            return;

        if (!_appearance.TryGetData<bool>(uid, PowerDeviceVisuals.Powered, out var powered, args.Component) || !powered)
            return;

        if (!_appearance.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, args.Component))
            return;

        _appearance.TryGetData<bool>(uid, DoorVisuals.BoltLights, out var boltedLights, args.Component);
        _appearance.TryGetData<bool>(uid, DoorVisuals.EmergencyLights, out var emergencyLights, args.Component);

        if (boltedLights || emergencyLights)
            return;

        if (state == DoorState.Open)
        {
            if (!HasRsiState(args.Sprite, comp.OpenUnlitState))
                return;

            _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BaseUnlit, true);
            _sprite.LayerSetRsiState((uid, args.Sprite), DoorVisualLayers.BaseUnlit, comp.OpenUnlitState);
        }
        else if (state == DoorState.Closed)
        {
            if (!HasRsiState(args.Sprite, comp.ClosedUnlitState))
                return;

            _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BaseUnlit, true);
            _sprite.LayerSetRsiState((uid, args.Sprite), DoorVisualLayers.BaseUnlit, comp.ClosedUnlitState);
        }
    }

    private static bool HasRsiState(SpriteComponent sprite, string stateName)
    {
        var rsi = sprite.BaseRSI;
        return rsi != null && rsi.TryGetState(stateName, out _);
    }
}
