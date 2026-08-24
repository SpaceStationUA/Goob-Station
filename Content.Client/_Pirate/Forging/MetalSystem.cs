// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Forging;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Forging;

public sealed class MetalSystem : SharedMetalSystem
{
    private static readonly ProtoId<ShaderPrototype> EmissiveShader = "Emissive";

    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _itemQuery = GetEntityQuery<ItemComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        SubscribeLocalEvent<MetallicComponent, ComponentStartup>(OnMetalStartup);
        SubscribeLocalEvent<SpriteComponent, MetalChangedEvent>(OnSpriteChanged);
    }

    private void OnMetalStartup(Entity<MetallicComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Metal is { } metal)
            UpdateSprites(ent.Owner, _prototypes.Index(metal));
    }

    private void OnSpriteChanged(Entity<SpriteComponent> ent, ref MetalChangedEvent args)
    {
        UpdateSprites(ent.AsNullable(), args.Metal);
    }

    private void UpdateSprites(Entity<SpriteComponent?> ent, MetalPrototype prototype)
    {
        if (!_spriteQuery.Resolve(ent, ref ent.Comp))
            return;

        if (_sprite.LayerMapTryGet(ent, MetallicVisuals.Layer, out var index, false))
            _sprite.LayerSetColor(ent, index, prototype.Color);

        if (_itemQuery.TryComp(ent, out var item))
            _item.SetInhandLayerColorByShader((ent.Owner, item), EmissiveShader.Id, prototype.Color);
    }
}
