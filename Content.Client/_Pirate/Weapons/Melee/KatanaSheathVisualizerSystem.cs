using Content.Client.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Item;
using Content.Shared._Pirate.Weapons.Melee;
using Content.Shared._Pirate.Weapons.Melee.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Weapons.Melee;

public sealed class KatanaSheathVisualizerSystem : VisualizerSystem<KatanaSheathComponent>
{
    private const string InventoryHandleLayer = "katana-sheath-handle";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatanaSheathComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid, KatanaSheathComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var index = _sprite.LayerMapReserve((uid, args.Sprite), InventoryHandleLayer);

        if (_appearance.TryGetData<PrototypeLayerData>(uid, KatanaSheathVisuals.InventoryHandle, out var layerData, args.Component))
        {
            _sprite.LayerSetData((uid, args.Sprite), index, CloneLayer(layerData));
            _sprite.LayerSetVisible((uid, args.Sprite), index, true);
        }
        else
        {
            _sprite.LayerSetVisible((uid, args.Sprite), index, false);
        }

        _item.VisualsChanged(uid);
    }

    private void OnGetEquipmentVisuals(EntityUid uid, KatanaSheathComponent component, ref GetEquipmentVisualsEvent args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var appearanceKey = args.Slot switch
        {
            "belt" => KatanaSheathVisuals.BeltHandle,
            "back" => KatanaSheathVisuals.BackpackHandle,
            _ => (KatanaSheathVisuals?) null,
        };

        if (appearanceKey == null ||
            !_appearance.TryGetData<PrototypeLayerData>(uid, appearanceKey.Value, out var layerData, appearance))
        {
            return;
        }

        args.Layers.Add(($"{args.Slot}-katana-sheath-handle", CloneLayer(layerData)));
    }

    private static PrototypeLayerData CloneLayer(PrototypeLayerData layer)
    {
        return new PrototypeLayerData
        {
            Shader = layer.Shader,
            TexturePath = layer.TexturePath,
            RsiPath = layer.RsiPath,
            State = layer.State,
            Scale = layer.Scale,
            Rotation = layer.Rotation,
            Offset = layer.Offset,
            Visible = layer.Visible,
            Color = layer.Color,
            MapKeys = layer.MapKeys == null ? null : new(layer.MapKeys),
            RenderingStrategy = layer.RenderingStrategy,
            CopyToShaderParameters = layer.CopyToShaderParameters,
            Cycle = layer.Cycle,
        };
    }
}
