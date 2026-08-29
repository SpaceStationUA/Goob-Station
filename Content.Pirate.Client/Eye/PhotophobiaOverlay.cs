// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.Photo;
using Content.Pirate.Shared.Traits;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Eye;

public sealed class PhotophobiaOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private const float MaxStrengthMultiplier = 1.25f;
    private const float MinStrengthMultiplier = 0f;
    private static readonly ProtoId<ShaderPrototype> Shader = "PhotophobiaShader";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _photophobiaShader;
    private readonly PhotoCaptureFilterSystem _photoCaptureFilter;

    public PhotophobiaOverlay()
    {
        IoCManager.InjectDependencies(this);
        _photophobiaShader = _prototypeManager.Index(Shader).InstanceUnique();
        _photoCaptureFilter = _entityManager.System<PhotoCaptureFilterSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_photoCaptureFilter.IsSuppressedForEye(args.Viewport.Eye, PhotoCaptureSuppressionMask.VisionEffects))
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;
        if (playerEntity == null ||
            !_entityManager.TryGetComponent<PhotophobiaComponent>(playerEntity, out var photophobia))
        {
            return;
        }

        _photophobiaShader.SetParameter("SCREEN_TEXTURE", args.Viewport.RenderTarget.Texture);
        _photophobiaShader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        _photophobiaShader.SetParameter(
            "effectStrength",
            Math.Clamp(photophobia.ShaderStrengthMultiplier, MinStrengthMultiplier, MaxStrengthMultiplier));

        var worldHandle = args.WorldHandle;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_photophobiaShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
