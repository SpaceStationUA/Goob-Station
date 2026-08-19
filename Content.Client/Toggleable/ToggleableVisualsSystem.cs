using System.Linq;
using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Humanoid; // Pirate
using Content.Shared.Humanoid.Prototypes; // Pirate
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes; // Pirate
using Robust.Shared.Utility;

namespace Content.Client.Toggleable;

/// <summary>
/// Implements the behavior of <see cref="ToggleableVisualsComponent"/> by reacting to
/// <see cref="AppearanceChangeEvent"/>, for the sprite directly; <see cref="OnGetHeldVisuals"/> for the
/// in-hand visuals; and <see cref="OnGetEquipmentVisuals"/> for the clothing visuals.
/// </summary>
/// <see cref="ToggleableVisualsComponent"/>
public sealed class ToggleableVisualsSystem : VisualizerSystem<ToggleableVisualsComponent>
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // Pirate

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleableVisualsComponent, GetInhandVisualsEvent>(OnGetHeldVisuals,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<ToggleableVisualsComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid,
        ToggleableVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, args.Component))
            return;

        var modulateColor =
            AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, args.Component);

        // Update the item's sprite
        if (args.Sprite != null && component.SpriteLayer != null &&
            SpriteSystem.LayerMapTryGet((uid, args.Sprite), component.SpriteLayer, out var layer, false))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, enabled);
            if (modulateColor)
                SpriteSystem.LayerSetColor((uid, args.Sprite), component.SpriteLayer, color);

            if (component.ReplaceMode && args.Sprite.AllLayers.Any()) // Pirate
            {
                SpriteSystem.LayerSetVisible((uid, args.Sprite), 0, !enabled);
            }
        }

        // If there's a `ItemTogglePointLightComponent` that says to apply the color to attached lights, do so.
        if (TryComp<ItemTogglePointLightComponent>(uid, out var toggleLights) &&
            TryComp(uid, out PointLightComponent? light))
        {
            DebugTools.Assert(!light.NetSyncEnabled,
                $"{typeof(ItemTogglePointLightComponent)} requires point lights without net-sync");
            _pointLight.SetEnabled(uid, enabled, light);
            if (modulateColor && toggleLights.ToggleableVisualsColorModulatesLights)
            {
                _pointLight.SetColor(uid, color, light);
            }
        }

        // update clothing & in-hand visuals.
        _item.VisualsChanged(uid);
    }

    private void OnGetEquipmentVisuals(EntityUid uid,
        ToggleableVisualsComponent component,
        GetEquipmentVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance)
            || !AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, appearance)
            || !enabled)
            return;

        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;
        List<PrototypeLayerData>? layers = null;

        // Pirate edit start - clothing fallback
        var speciesId = inventory.SpeciesId ?? CompOrNull<HumanoidAppearanceComponent>(args.Equipee)?.Species.ToString();

        // Try the equipped species first, then its optional clothing fallback.
        if (speciesId != null)
        {
            foreach (var species in GetClothingSpecies(speciesId, args.Slot))
            {
                if (component.ClothingVisuals.TryGetValue($"{args.Slot}-{species}", out layers))
                    break;
            }
        }
        // Pirate edit - clothing fallback

        // No species specific data.  Try to default to generic data.
        if (layers == null && !component.ClothingVisuals.TryGetValue(args.Slot, out layers))
            return;

        // Pirate
        if (component.ReplaceMode)
        {
            for (var layerIdx = args.Layers.Count - 1; layerIdx >= 0; layerIdx--)
            {
                var (layerKey, _) = args.Layers[layerIdx];
                if (layerKey.StartsWith($"{args.Slot}-") && !layerKey.Contains("-toggle"))
                {
                    args.Layers.RemoveAt(layerIdx);
                }
            }
        }
        // Pirate end

        var modulateColor = AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, appearance);

        var i = 0;
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? $"{args.Slot}-toggle" : $"{args.Slot}-toggle-{i}";
                i++;
            }

            if (modulateColor)
                layer.Color = color;

            args.Layers.Add((key, layer));
        }
    }

    private void OnGetHeldVisuals(EntityUid uid, ToggleableVisualsComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance)
            || !AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, appearance)
            || !enabled)
            return;

        if (!component.InhandVisuals.TryGetValue(args.Location, out var layers))
            return;

        // Pirate
        if (component.ReplaceMode)
        {
            var prefix = $"inhand-{args.Location.ToString().ToLowerInvariant()}";
            for (var layerIdx = args.Layers.Count - 1; layerIdx >= 0; layerIdx--)
            {
                var (layerKey, _) = args.Layers[layerIdx];
                if (layerKey.StartsWith(prefix) && !layerKey.Contains("-toggle"))
                {
                    args.Layers.RemoveAt(layerIdx);
                }
            }
        }
        // Pirate end

        var modulateColor = AppearanceSystem.TryGetData<Color>(uid, ToggleableVisuals.Color, out var color, appearance);

        var i = 0;
        var defaultKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}-toggle";
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? defaultKey : $"{defaultKey}-{i}";
                i++;
            }

            if (modulateColor)
                layer.Color = color;

            args.Layers.Add((key, layer));
        }
    }

    // Pirate edit start - clothing fallback
    private IEnumerable<string> GetClothingSpecies(string speciesId, string slot)
    {
        yield return speciesId;

        var normalizedSpeciesId = speciesId.ToLowerInvariant();
        if (normalizedSpeciesId != speciesId)
            yield return normalizedSpeciesId;

        var species = _prototypeManager.EnumeratePrototypes<SpeciesPrototype>()
            .FirstOrDefault(p => string.Equals(p.ID, speciesId, StringComparison.OrdinalIgnoreCase));
        if (species is not null
            && species.ClothingSpeciesFallback.FirstOrDefault(p => string.Equals(p.Key, slot, StringComparison.OrdinalIgnoreCase)) is { Key: not null, Value: var fallback }
            && !string.Equals(fallback.ToString(), speciesId, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackId = fallback.ToString();
            yield return fallbackId;

            if (fallbackId.ToLowerInvariant() != fallbackId)
                yield return fallbackId.ToLowerInvariant();
        }
    }
    // Pirate edit - clothing fallback
}
