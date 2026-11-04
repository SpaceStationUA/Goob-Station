// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using System.Linq;
using Content.Shared._Pirate.Crayon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Crayon;

public sealed class CrayonStampVisualSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, OwnedTexture> _textures = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CrayonStampComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CrayonStampComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<CrayonStampComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ent.Comp.Symbol.IsValid() || !TryComp(ent, out SpriteComponent? sprite))
            return;

        Release(ent);
        var texture = CrayonSymbolImage.ToTexture(_clyde, ent.Comp.Symbol);
        _textures[ent] = texture;

        if (sprite.AllLayers.Any())
            _sprite.LayerSetTexture((ent, sprite), 0, texture);
        else
            _sprite.AddTextureLayer((ent, sprite), texture);
    }

    private void OnShutdown(Entity<CrayonStampComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out SpriteComponent? sprite) && sprite.AllLayers.Any())
            _sprite.LayerSetVisible((ent, sprite), 0, false);

        Release(ent);
    }

    private void Release(EntityUid uid)
    {
        if (_textures.Remove(uid, out var texture))
            texture.Dispose();
    }
}
