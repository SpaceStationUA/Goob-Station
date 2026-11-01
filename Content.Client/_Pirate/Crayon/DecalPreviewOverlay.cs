// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.SprayPainter.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Crayon;

public sealed class DecalPreviewOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly SharedChargesSystem _charges;
    private readonly SharedHandsSystem _hands;
    private readonly SharedInteractionSystem _interaction;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly TurfSystem _turf;
    private readonly CrayonStencilSystem _stencil;
    private readonly IClyde _clyde;
    private CrayonSymbol? _previewSymbol;
    private OwnedTexture? _symbolTexture;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public DecalPreviewOverlay()
    {
        IoCManager.InjectDependencies(this);

        _charges = _entity.System<SharedChargesSystem>();
        _hands = _entity.System<SharedHandsSystem>();
        _interaction = _entity.System<SharedInteractionSystem>();
        _map = _entity.System<SharedMapSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
        _turf = _entity.System<TurfSystem>();
        _stencil = _entity.System<CrayonStencilSystem>();
        _clyde = IoCManager.Resolve<IClyde>();

        ZIndex = 1000;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var opacity = _cfg.GetCVar(CCVars.PirateDecalPreviewOpacity);
        if (opacity <= 0f
            || _player.LocalEntity is not { } user
            || _hands.GetActiveItem(user) is not { } held
            || GetPreview(user, held) is not { } preview)
            return;

        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mousePos.MapId != args.MapId
            || !_mapManager.TryFindGridAt(mousePos, out var gridUid, out var grid)
            || _turf.IsSpace(_map.GetTileRef(gridUid, grid, mousePos))
            || !_interaction.InRangeUnobstructed(user, mousePos))
            return;

        var localPos = Vector2.Transform(mousePos.Position, _transform.GetInvWorldMatrix(gridUid));
        if (preview.Snap)
            localPos = localPos.Floored() + grid.TileSizeHalfVector;

        var handle = args.WorldHandle;
        handle.SetTransform(_transform.GetWorldMatrix(gridUid));
        // Pirate: use the actual texture dimensions, just like DecalOverlay and the custom stamp's sprite.
        handle.DrawTexture(preview.Texture, localPos + preview.Offset, preview.Rotation,
            preview.Color * Color.White.WithAlpha(Math.Clamp(opacity, 0f, 1f)));
        handle.SetTransform(Matrix3x2.Identity);
    }

    private DecalPreview? GetPreview(EntityUid user, EntityUid held)
    {
        if (_entity.TryGetComponent<CrayonComponent>(held, out var crayon))
        {
            if (!_cfg.GetCVar(CCVars.PirateCrayonPreview))
                return null;

            var rotation = _entity.TryGetComponent<CrayonRotationComponent>(held, out var rotator)
                ? rotator.Rotation
                : Angle.Zero;
            // Pirate: the player's saved drawing replaces the stock glyph in the same preview.
            if (_stencil.TryGetStencil(user, out var symbol))
            {
                if (!ReferenceEquals(symbol, _previewSymbol))
                {
                    ReleaseSymbol();
                    _previewSymbol = symbol;
                    _symbolTexture = CrayonSymbolImage.ToTexture(_clyde, symbol);
                }

                if (_symbolTexture == null)
                    return null;

                var half = (Vector2) _symbolTexture.Size / (2f * EyeManager.PixelsPerMeter);
                return new DecalPreview(_symbolTexture, Color.White, rotation, false, -half);
            }

            ReleaseSymbol();
            if (_charges.IsEmpty(held) || !_prototype.TryIndex<DecalPrototype>(crayon.SelectedState, out var decal))
                return null;

            return new DecalPreview(_sprite.Frame0(decal.Sprite), crayon.Color, rotation, false, new Vector2(-0.5f));
        }

        ReleaseSymbol();
        if (!_entity.TryGetComponent<SprayPainterComponent>(held, out var painter)
            || painter.DecalMode != DecalPaintMode.Add
            || _entity.TryGetComponent<LimitedChargesComponent>(held, out var charges)
            && _charges.GetCurrentCharges((held, charges)) < painter.DecalChargeCost
            || !_prototype.TryIndex(painter.SelectedDecal, out var sprayDecal))
            return null;

        return new DecalPreview(_sprite.Frame0(sprayDecal.Sprite),
            painter.SelectedDecalColor ?? Color.White,
            Angle.FromDegrees(painter.SelectedDecalAngle),
            painter.SnapDecals,
            new Vector2(-0.5f));
    }

    private void ReleaseSymbol()
    {
        _symbolTexture?.Dispose();
        _symbolTexture = null;
        _previewSymbol = null;
    }

    protected override void DisposeBehavior()
    {
        ReleaseSymbol();
        base.DisposeBehavior();
    }

    private readonly record struct DecalPreview(
        Texture Texture,
        Color Color,
        Angle Rotation,
        bool Snap,
        Vector2 Offset);
}
