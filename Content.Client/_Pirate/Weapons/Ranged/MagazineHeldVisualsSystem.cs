// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._Pirate.Weapons.Ranged.Components;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Pirate.Weapons.Ranged;

/// <inheritdoc cref="MagazineHeldVisualsComponent"/>
public sealed class MagazineHeldVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MagazineHeldVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<MagazineHeldVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<MagazineHeldVisualsComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
    }

    private void OnAppearanceChange(Entity<MagazineHeldVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        var loaded = IsLoaded(ent);
        if (ent.Comp.LastLoaded == loaded)
            return;

        ent.Comp.LastLoaded = loaded;
        _item.VisualsChanged(ent);
    }

    private void OnGetInhandVisuals(Entity<MagazineHeldVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!IsLoaded(ent))
            SwapToEmpty(ent, args.Layers);
    }

    private void OnGetEquipmentVisuals(Entity<MagazineHeldVisualsComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!IsLoaded(ent))
            SwapToEmpty(ent, args.Layers);
    }

    private bool IsLoaded(EntityUid uid)
    {
        // Guns that never reported a magazine state are drawn as loaded, matching MagazineVisuals.
        return !_appearance.TryGetData<bool>(uid, AmmoVisuals.MagLoaded, out var loaded) || loaded;
    }

    private void SwapToEmpty(Entity<MagazineHeldVisualsComponent> ent, List<(string, PrototypeLayerData)> layers)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            var (key, layer) = layers[i];
            if (layer.State == null || layer.RsiPath == null)
                continue;

            var state = $"{layer.State}-{ent.Comp.EmptySuffix}";
            if (!_resCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath, out var rsi) ||
                !rsi.RSI.TryGetState(state, out _))
            {
                continue;
            }

            // Layers can come straight from prototype data, so never mutate them in place.
            layers[i] = (key, new PrototypeLayerData
            {
                Shader = layer.Shader,
                TexturePath = layer.TexturePath,
                RsiPath = layer.RsiPath,
                State = state,
                Scale = layer.Scale,
                Rotation = layer.Rotation,
                Offset = layer.Offset,
                Visible = layer.Visible,
                Color = layer.Color,
                MapKeys = layer.MapKeys,
                RenderingStrategy = layer.RenderingStrategy,
                CopyToShaderParameters = layer.CopyToShaderParameters,
                Cycle = layer.Cycle,
                Loop = layer.Loop,
            });
        }
    }
}
