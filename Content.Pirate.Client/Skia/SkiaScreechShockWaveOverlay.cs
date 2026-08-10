// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.Skia;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Skia;

/// <summary>
/// Skia-specific port of the RMC screech screen-distortion effect.
/// </summary>
public sealed class SkiaScreechShockWaveOverlay : Overlay, IEntityEventSubscriber
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private SharedTransformSystem? _transform;
    private readonly ShaderInstance _shader;

    private Vector2 _position;
    private float _waveStrength;
    private float _waveSpeed;
    private float _downScale;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public SkiaScreechShockWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>("SkiaScreechShockWave").Instance().Duplicate();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || _transform is null && !_entityManager.TrySystem(out _transform))
            return false;

        var query = _entityManager.EntityQueryEnumerator<SkiaScreechShockWaveComponent, TransformComponent>();
        if (!query.MoveNext(out var uid, out var effect, out var transform))
            return false;

        if (transform.MapID != args.MapId)
            return false;

        var mapPosition = _transform.GetWorldPosition(uid);
        var localPosition = args.Viewport.WorldToLocal(mapPosition);
        localPosition.Y = 1 - localPosition.Y / args.Viewport.Size.Y;
        localPosition.X /= args.Viewport.Size.X;

        _position = localPosition;
        _waveStrength = effect.WaveStrength;
        _waveSpeed = effect.WaveSpeed;
        _downScale = effect.DownScale;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader.SetParameter("position", _position);
        _shader.SetParameter("waveSpeed", _waveSpeed);
        _shader.SetParameter("downScale", _downScale);
        _shader.SetParameter("waveStrength", _waveStrength);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
