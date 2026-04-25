using Content.Client.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Item;
using Content.Shared.Storage.Components;
using Content.Shared._Pirate.Weapons.Melee.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Weapons.Melee;

/// <summary>
/// Mirrors ItemMapper layers onto equipped clothing states when the RSI provides
/// corresponding equipped sprites such as "layer-equipped-BELT".
/// Requires <see cref="ItemMapperClothingVisualizerComponent"/> on the entity alongside
/// <c>ItemMapperComponent</c> and <c>AppearanceComponent</c>.
/// </summary>
public sealed class ItemMapperClothingVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    private static readonly Dictionary<string, string> SlotMap = new()
    {
        { "head", "HELMET" },
        { "eyes", "EYES" },
        { "ears", "EARS" },
        { "mask", "MASK" },
        { "outerClothing", "OUTERCLOTHING" },
        { ClientClothingSystem.Jumpsuit, "INNERCLOTHING" },
        { "neck", "NECK" },
        { "back", "BACKPACK" },
        { "belt", "BELT" },
        { "gloves", "HAND" },
        { "shoes", "FEET" },
        { "id", "IDCARD" },
    };

    public override void Initialize()
    {
        base.Initialize();
        // Subscribe on the marker component to avoid duplicate-subscription conflicts
        // with SharedItemMapperSystem (which subscribes on ItemMapperComponent).
        // AppearanceChangeEvent fires after SetData, so ShowLayerData is already current.
        SubscribeLocalEvent<ItemMapperClothingVisualizerComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<ItemMapperClothingVisualizerComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    private void OnAppearanceChange(Entity<ItemMapperClothingVisualizerComponent> ent, ref AppearanceChangeEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnGetEquipmentVisuals(Entity<ItemMapperClothingVisualizerComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance) ||
            !TryComp<ClothingComponent>(ent, out var clothing) ||
            !TryComp<SpriteComponent>(ent, out var sprite) ||
            sprite.BaseRSI == null ||
            !_appearance.TryGetData<ShowLayerData>(ent, StorageMapVisuals.LayerChanged, out var shownLayers, appearance))
        {
            return;
        }

        var correctedSlot = SlotMap.GetValueOrDefault(args.Slot, args.Slot.ToUpperInvariant());
        var rsiPath = clothing.RsiPath ?? sprite.BaseRSI.Path.ToString();

        foreach (var layerName in shownLayers.QueuedEntities)
        {
            var equippedState = $"{layerName}-equipped-{correctedSlot}";
            if (!sprite.BaseRSI.TryGetState(equippedState, out _))
                continue;

            args.Layers.Add((equippedState, new PrototypeLayerData
            {
                RsiPath = rsiPath,
                State = equippedState,
            }));
        }
    }
}
