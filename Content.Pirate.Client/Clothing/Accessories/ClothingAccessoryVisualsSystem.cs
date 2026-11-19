// SPDX-License-Identifier: AGPL-3.0-or-later
// Based on the accessory visuals of space_station_ADT (ADTAccessoryVisualsSystem, AGPL-3.0).

using System.Numerics;
using Content.Client.Clothing;
using Content.Client.DisplacementMap;
using Content.Goobstation.Common.Clothing;
using Content.Pirate.Shared.Clothing.Accessories;
using Content.Shared._Pirate.Loadouts;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Clothing.Accessories;

public sealed class ClothingAccessoryVisualsSystem : EntitySystem
{
    [Dependency] private ClothingAccessorySystem _accessory = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string IconLayerPrefix = "pirate-accessory-icon-";

    private static readonly Dictionary<string, string> SlotStates = new()
    {
        { "head", "HELMET" },
        { "neck", "NECK" },
        { "gloves", "HAND" },
        { "jumpsuit", "INNERCLOTHING" },
        { "outerClothing", "OUTERCLOTHING" },
    };

    public override void Initialize()
    {
        base.Initialize();

        // Accessory layers must follow clothing layers.
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EquipmentVisualsUpdatedEvent>(OnVisualsUpdated);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ComponentStartup>(OnHolderStartup);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ClothingAccessoriesChangedEvent>(OnAccessoriesChanged);
    }

    #region Worn

    private void OnGetVisuals(Entity<ClothingAccessoryHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        var i = 0;
        foreach (var (layer, _) in GetLayers(ent, args.Equipee))
        {
            args.Layers.Add((LayerKey(args.Slot, i), layer));
            i++;
        }
    }

    private static string LayerKey(string slot, int index) => $"{slot}-pirate-accessory-{index}";

    private void OnVisualsUpdated(Entity<ClothingAccessoryHolderComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (!TryComp<SpriteComponent>(args.Equipee, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (args.Equipee, spriteComp);
        var layers = GetLayers(ent, args.Equipee);

        for (var i = 0; i < layers.Count; i++)
        {
            var (data, category) = layers[i];
            var drawAt = category.DrawAt;
            var key = LayerKey(args.Slot, i);
            if (drawAt == null || !args.RevealedLayers.Contains(key))
                continue;

            _sprite.RemoveLayer(sprite, key, false);
            _sprite.RemoveLayer(sprite, $"{key}-displacement", false);

            if (!_sprite.LayerMapTryGet(sprite, drawAt, out var index, false))
                continue;

            _sprite.AddBlankLayer((args.Equipee, spriteComp), index);
            _sprite.LayerMapSet(sprite, key, index);
            _sprite.LayerSetData(sprite, index, data);

            if (_inventory.TryGetSlot(args.Equipee, drawAt, out var slotDef) &&
                spriteComp[index] is SpriteComponent.Layer layer)
            {
                _sprite.LayerSetOffset(layer, layer.Offset + slotDef.Offset);
            }

            var hidden = new CheckClothingSlotHiddenEvent(drawAt);
            RaiseLocalEvent(args.Equipee, ref hidden);
            _sprite.LayerSetVisible(sprite, index, hidden.Visible);

            if (GetDisplacement(args.Equipee, drawAt) is { } displacement &&
                !(data.State is { } state && Comp<InventoryComponent>(args.Equipee).SpeciesId is { } species && state.EndsWith(species)) &&
                _displacement.TryAddDisplacement(displacement, (args.Equipee, spriteComp), index, key, out var displacementKey))
            {
                args.RevealedLayers.Add(displacementKey);
            }
        }
    }

    private DisplacementData? GetDisplacement(EntityUid equipee, string slot)
    {
        if (!TryComp<InventoryComponent>(equipee, out var inventory))
            return null;

        var data = inventory.Displacements.GetValueOrDefault(slot);
        switch (CompOrNull<HumanoidAppearanceComponent>(equipee)?.Sex)
        {
            case Sex.Male when inventory.MaleDisplacements.Count > 0:
                data = inventory.MaleDisplacements.GetValueOrDefault(slot);
                break;
            case Sex.Female when inventory.FemaleDisplacements.Count > 0:
                data = inventory.FemaleDisplacements.GetValueOrDefault(slot);
                break;
        }

        return data;
    }

    #endregion

    #region Item icon

    private void OnHolderStartup(Entity<ClothingAccessoryHolderComponent> ent, ref ComponentStartup args)
    {
        UpdateIcon(ent);
    }

    private void OnAccessoriesChanged(Entity<ClothingAccessoryHolderComponent> ent, ref ClothingAccessoriesChangedEvent args)
    {
        UpdateIcon(ent);
    }

    private void UpdateIcon(Entity<ClothingAccessoryHolderComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);

        for (var i = 0; _sprite.LayerMapTryGet(sprite, IconLayerPrefix + i, out _, false); i++)
        {
            _sprite.RemoveLayer(sprite, IconLayerPrefix + i);
        }

        var n = 0;
        foreach (var (layer, category) in GetLayers(ent, null))
        {
            if (category.IconOffset != Vector2.Zero)
                layer.Offset = (layer.Offset ?? default) + category.IconOffset / EyeManager.PixelsPerMeter;

            var index = _sprite.AddLayer(sprite, layer, null);
            _sprite.LayerMapSet(sprite, IconLayerPrefix + n, index);
            _sprite.LayerSetDirOffset(sprite, index, GetDirOffset(category.IconDirection));
            n++;
        }
    }

    private static SpriteComponent.DirectionOffset GetDirOffset(Direction direction)
    {
        return direction switch
        {
            Direction.West => SpriteComponent.DirectionOffset.Clockwise,
            Direction.East => SpriteComponent.DirectionOffset.CounterClockwise,
            Direction.North => SpriteComponent.DirectionOffset.Flip,
            _ => SpriteComponent.DirectionOffset.None,
        };
    }

    #endregion

    private List<(PrototypeLayerData Layer, ClothingAccessoryCategoryPrototype Category)> GetLayers(Entity<ClothingAccessoryHolderComponent> ent, EntityUid? equipee)
    {
        var result = new List<(PrototypeLayerData, ClothingAccessoryCategoryPrototype)>();
        var perCategory = new Dictionary<string, int>();
        var layers = new List<PrototypeLayerData>();

        foreach (var accessory in _accessory.GetAccessories(ent))
        {
            if (!TryComp<ClothingAccessoryComponent>(accessory, out var comp) ||
                _accessory.GetCategory(comp) is not { } category)
                continue;

            var index = perCategory.GetValueOrDefault(category.ID);
            perCategory[category.ID] = index + 1;

            layers.Clear();
            GetAccessoryLayers(accessory, comp, category, equipee, layers);

            foreach (var layer in layers)
            {
                if (category.Offsets.Count > 0)
                {
                    var offset = category.Offsets[Math.Min(index, category.Offsets.Count - 1)] / EyeManager.PixelsPerMeter;
                    layer.Offset = (layer.Offset ?? default) + offset;
                }

                result.Add((layer, category));
            }
        }

        return result;
    }

    private void GetAccessoryLayers(EntityUid accessory,
        ClothingAccessoryComponent comp,
        ClothingAccessoryCategoryPrototype category,
        EntityUid? equipee,
        List<PrototypeLayerData> layers)
    {
        // Match the clothing system's item RSI fallback.
        var itemRsi = CompOrNull<SpriteComponent>(accessory)?.BaseRSI?.Path.ToString();

        if (comp.EquippedState is { } state)
        {
            AddStateLayer(GetRsiPath(accessory, comp), state, layers);
            return;
        }

        if (equipee != null)
        {
            var ev = new GetEquipmentVisualsEvent(equipee.Value, category.Slot);
            RaiseLocalEvent(accessory, ev);

            foreach (var (_, layer) in ev.Layers)
            {
                layers.Add(Copy(layer, itemRsi));
            }

            return;
        }

        if (!TryComp<ClothingComponent>(accessory, out var clothing))
            return;

        if (clothing.ClothingVisuals.TryGetValue(category.Slot, out var visuals))
        {
            foreach (var layer in visuals)
            {
                layers.Add(Copy(layer, itemRsi));
            }
        }
        else
        {
            var slotState = SlotStates.GetValueOrDefault(category.Slot, category.Slot.ToUpperInvariant());
            var defaultState = clothing.EquippedState ?? (string.IsNullOrEmpty(clothing.EquippedPrefix)
                ? $"equipped-{slotState}"
                : $"{clothing.EquippedPrefix}-equipped-{slotState}");
            var rsi = clothing.RsiPath != null ? SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath : (ResPath?) null;
            AddStateLayer(rsi ?? (itemRsi != null ? new ResPath(itemRsi) : null), defaultState, layers);
        }

        if (TryComp<LoadoutTintComponent>(accessory, out var tint))
        {
            foreach (var layer in layers)
            {
                layer.Color = tint.Color;
            }
        }
    }

    private void AddStateLayer(ResPath? path, string state, List<PrototypeLayerData> layers)
    {
        if (path is { } rsiPath &&
            _cache.TryGetResource<RSIResource>(rsiPath, out var rsi) &&
            rsi.RSI.TryGetState(state, out _))
        {
            layers.Add(new PrototypeLayerData { RsiPath = rsiPath.ToString(), State = state });
        }
    }

    private static PrototypeLayerData Copy(PrototypeLayerData layer, string? itemRsi)
    {
        // Do not apply map keys from the accessory's own slot to the wearer.
        return new PrototypeLayerData
        {
            Shader = layer.Shader,
            TexturePath = layer.TexturePath,
            RsiPath = layer.RsiPath ?? (layer.TexturePath == null ? itemRsi : null),
            State = layer.State,
            Scale = layer.Scale,
            Rotation = layer.Rotation,
            Offset = layer.Offset,
            Visible = layer.Visible,
            Color = layer.Color,
            RenderingStrategy = layer.RenderingStrategy,
            CopyToShaderParameters = layer.CopyToShaderParameters,
            Cycle = layer.Cycle,
            Loop = layer.Loop,
        };
    }

    private ResPath? GetRsiPath(EntityUid accessory, ClothingAccessoryComponent comp)
    {
        if (comp.Sprite is { } sprite)
            return SpriteSpecifierSerializer.TextureRoot / sprite;

        if (TryComp<ClothingComponent>(accessory, out var clothing) && clothing.RsiPath != null)
            return SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath;

        if (TryComp<SpriteComponent>(accessory, out var spriteComp) && spriteComp.BaseRSI != null)
            return spriteComp.BaseRSI.Path;

        return null;
    }
}
