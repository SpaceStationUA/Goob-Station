// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared._Pirate.Crayon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Crayon;

public sealed class CrayonStampVisualSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private enum StampLayer
    {
        Drawing,
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<CrayonStampComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CrayonStampComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<CrayonStampComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (!ent.Comp.Symbol.IsValid())
        {
            _sprite.RemoveLayer((ent, sprite), StampLayer.Drawing, logMissing: false);
            return;
        }

        // Sprite copies and UI icons can outlive the stamp. Clyde releases the texture
        // on its graphics thread once all of those references are gone.
        var index = _sprite.LayerMapReserve((ent, sprite), StampLayer.Drawing);
        _sprite.LayerSetTexture((ent, sprite), index, CrayonSymbolImage.ToTexture(_clyde, ent.Comp.Symbol));
    }

    private void OnShutdown(Entity<CrayonStampComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out SpriteComponent? sprite))
            _sprite.RemoveLayer((ent, sprite), StampLayer.Drawing, logMissing: false);
    }
}
