// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.WeldingVisor;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - darkens the edges of the screen while the local player has a lowered welding
/// visor equipped, based on tgstation's "impaired" fullscreen overlay for welding gear. Uses tgstation's
/// strongest tier (impairedoverlay3) drawn at reduced opacity, landing between their darkened and strongest
/// tiers - a tighter viewing window than plain TINT_DARKENED without blacking the periphery out completely.
/// </summary>
public sealed class WeldingVisorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>Screen edge opacity.</summary>
    private const float Opacity = 0.7f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly Texture _texture;

    public WeldingVisorOverlay()
    {
        IoCManager.InjectDependencies(this);

        var spriteSys = _entityManager.System<SpriteSystem>();
        _texture = spriteSys.Frame0(new SpriteSpecifier.Texture(new ResPath("/Textures/_Pirate/Overlays/WeldingVisor/impaired.png")));
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        // Pirate: welding visor toggle - the component stays on the wearer for as long as any welding visor is
        // worn (added/removed only on equip/unequip); Sources tracks which of those are actually lowered right now.
        return _entityManager.TryGetComponent(_playerManager.LocalEntity, out WeldingVisorImpairedComponent? impaired)
            && impaired.Sources.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.DrawTextureRect(_texture, args.WorldBounds, Color.White.WithAlpha(Opacity));
    }
}
