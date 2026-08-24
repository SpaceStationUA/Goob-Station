// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Forging;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Pirate.Forging;

/// <summary>
/// Sets generated forged-item sprites only when an item starts up or forging completes.
/// </summary>
public sealed class ForgingVisualsSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _cache = default!;
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
        SubscribeLocalEvent<ForgedItemComponent, ComponentStartup>(OnForgedStartup);
        SubscribeLocalEvent<SpriteComponent, ForgingCompletedEvent>(OnSpriteForged);
    }

    private void OnForgedStartup(Entity<ForgedItemComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Completed)
            UpdateSprites(ent.Owner, _prototypes.Index(ent.Comp.Item));
    }

    private void OnSpriteForged(Entity<SpriteComponent> ent, ref ForgingCompletedEvent args)
    {
        UpdateSprites(ent.AsNullable(), args.Item);
    }

    private void UpdateSprites(Entity<SpriteComponent?> ent, ForgedItemPrototype prototype)
    {
        if (!_spriteQuery.Resolve(ent, ref ent.Comp) || prototype.Sprite is not { } sprite)
            return;

        var path = SpriteSpecifierSerializer.TextureRoot / sprite;
        var rsi = _cache.GetResource<RSIResource>(path).RSI;
        _sprite.SetBaseRsi(ent, rsi);
        if (_itemQuery.TryComp(ent, out var item))
            _item.SetRsiPath((ent.Owner, item), sprite.ToString());
    }
}
