// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.Replicator;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Replicator;

public sealed class ReplicatorSignSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReplicatorSignComponent, ComponentStartup>(ReplicatorSignAdded);
        SubscribeLocalEvent<ReplicatorSignComponent, ComponentShutdown>(ReplicatorSignRemoved);
    }

    private void ReplicatorSignRemoved(Entity<ReplicatorSignComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent, sprite), ReplicatorSignKey.Key, out var layer, false))
            return;

        _sprite.RemoveLayer((ent, sprite), layer);
    }

    private void ReplicatorSignAdded(Entity<ReplicatorSignComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), ReplicatorSignKey.Key, out var _, false))
            return;

        var layer = _sprite.AddLayer((ent, sprite), new SpriteSpecifier.Rsi(ent.Comp.SpritePath, "sign"));
        _sprite.LayerMapSet((ent, sprite), ReplicatorSignKey.Key, layer);

        sprite.LayerSetShader(layer, "unshaded");
    }

    private enum ReplicatorSignKey
    {
        Key,
    }
}
