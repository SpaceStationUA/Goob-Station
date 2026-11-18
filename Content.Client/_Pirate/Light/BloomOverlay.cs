// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Light;

public sealed class BloomResources : IDisposable
{
    public const int MaxLevels = 8;

    public readonly IRenderTexture?[] Down = new IRenderTexture?[MaxLevels];
    public readonly IRenderTexture?[] Up = new IRenderTexture?[MaxLevels];

    public void Dispose()
    {
        for (var i = 0; i < MaxLevels; i++)
        {
            Down[i]?.Dispose();
            Down[i] = null;
            Up[i]?.Dispose();
            Up[i] = null;
        }
    }

    public static void EnsureTarget(IClyde clyde, ref IRenderTexture? target, Vector2i size, string name)
    {
        if (target?.Texture.Size == size)
            return;

        var previous = target;
        target = null;
        previous?.Dispose();
        // Float intermediates prevent banding in dim halos; the engine falls back if float framebuffers are unavailable.
        target = clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
            new TextureSampleParameters { Filter = true },
            name: name);
    }
}

public sealed class BloomOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> PrefilterShader = "PirateBloomPrefilter";
    private static readonly ProtoId<ShaderPrototype> DownsampleShader = "PirateBloomDownsample";
    private static readonly ProtoId<ShaderPrototype> UpsampleShader = "PirateBloomUpsample";
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "PirateBloomComposite";

    // Adjust level count so the halo keeps the same world-space radius at every render scale.
    private const int BaseLevels = 5;
    private const int MinLevels = 2;

    private const float KneeFraction = 0.5f;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly OverlayResourceCache<BloomResources> _resources = new();
    private readonly ShaderInstance _prefilter;
    private readonly ShaderInstance _downsample;
    private readonly ShaderInstance _upsample;
    private readonly ShaderInstance _composite;
    private readonly Vector2i[] _sizes = new Vector2i[BloomResources.MaxLevels];

    public float Intensity;
    public float Threshold;
    public float Spread;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public BloomOverlay()
    {
        IoCManager.InjectDependencies(this);
        _prefilter = _proto.Index(PrefilterShader).InstanceUnique();
        _downsample = _proto.Index(DownsampleShader).InstanceUnique();
        _upsample = _proto.Index(UpsampleShader).InstanceUnique();
        _composite = _proto.Index(CompositeShader).InstanceUnique();

        // Render before screen effects so they also apply to the glow.
        ZIndex = -2;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Intensity <= 0f || args.MapId == MapId.Nullspace)
            return false;

        // Draw only in the attached player's main view, not camera views or separate Z-level passes.
        if (!_entMan.TryGetComponent(_player.LocalSession?.AttachedEntity, out EyeComponent? eyeComp) ||
            eyeComp.Eye is not { } eye ||
            args.Viewport.Eye != eye)
        {
            return false;
        }

        // Without lighting, every floor is bright and the whole screen would glow.
        return _lightManager.Enabled && _lightManager.DrawLighting && eye.DrawLight;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (ScreenTexture == null || eye == null)
            return;

        // ScreenTexture.Size stays 1920x1080; the engine resizes only its GL storage, so use the viewport size.
        var size = args.Viewport.RenderTarget.Size;

        var renderPerWorldPixel = MathF.Max(args.Viewport.RenderScale.Y * eye.Scale.Y, 0.01f);
        var wanted = Math.Clamp(BaseLevels + (int) MathF.Round(MathF.Log2(renderPerWorldPixel)),
            MinLevels, BloomResources.MaxLevels);

        var count = 0;
        var levelSize = size;
        for (var i = 0; i < wanted; i++)
        {
            levelSize = new Vector2i((levelSize.X + 1) / 2, (levelSize.Y + 1) / 2);
            if (levelSize.X < 2 || levelSize.Y < 2)
                break;

            _sizes[i] = levelSize;
            count++;
        }

        if (count == 0)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new BloomResources());
        for (var i = 0; i < count; i++)
        {
            BloomResources.EnsureTarget(_clyde, ref res.Down[i], _sizes[i], $"pirate-bloom-down-{i}");
            if (i < count - 1)
                BloomResources.EnsureTarget(_clyde, ref res.Up[i], _sizes[i], $"pirate-bloom-up-{i}");
        }

        var handle = args.WorldHandle;

        // Use explicit FRAGCOORD scales because render-target quad UVs are not stable. Odd sizes round up at the edge.
        var threshold = Math.Clamp(Threshold, 0f, 1f);
        _prefilter.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _prefilter.SetParameter("UV_SCALE", new Vector2(2f) / size);
        _prefilter.SetParameter("SOURCE_TEXEL", Vector2.One / size);
        _prefilter.SetParameter("THRESHOLD", threshold);
        _prefilter.SetParameter("KNEE", MathF.Max(threshold * KneeFraction, 0.0001f));
        DrawPass(handle, res.Down[0]!, _sizes[0], _prefilter);

        for (var i = 1; i < count; i++)
        {
            var source = res.Down[i - 1]!;
            _downsample.SetParameter("SOURCE_TEXTURE", source.Texture);
            _downsample.SetParameter("UV_SCALE", new Vector2(2f) / _sizes[i - 1]);
            _downsample.SetParameter("SOURCE_TEXEL", Vector2.One / _sizes[i - 1]);
            DrawPass(handle, res.Down[i]!, _sizes[i], _downsample);
        }

        var spread = Math.Clamp(Spread, 0f, 2f);
        var lower = res.Down[count - 1]!;
        for (var i = count - 2; i >= 0; i--)
        {
            _upsample.SetParameter("CURRENT_TEXTURE", res.Down[i]!.Texture);
            _upsample.SetParameter("CURRENT_UV_SCALE", Vector2.One / _sizes[i]);
            _upsample.SetParameter("LOWER_TEXTURE", lower.Texture);
            _upsample.SetParameter("LOWER_UV_SCALE", new Vector2(0.5f) / _sizes[i + 1]);
            _upsample.SetParameter("LOWER_TEXEL", Vector2.One / _sizes[i + 1]);
            _upsample.SetParameter("WEIGHT", spread);
            DrawPass(handle, res.Up[i]!, _sizes[i], _upsample);
            lower = res.Up[i]!;
        }

        // Do not normalize the weighted sum; normalization makes the wider spread levels too faint.
        _composite.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _composite.SetParameter("BLOOM_TEXTURE", lower.Texture);
        _composite.SetParameter("BLOOM_UV_SCALE", new Vector2(0.5f) / _sizes[0]);
        _composite.SetParameter("BLOOM_TEXEL", Vector2.One / _sizes[0]);
        _composite.SetParameter("INTENSITY", Intensity);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_composite);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private static void DrawPass(DrawingHandleWorld handle, IRenderTexture target, Vector2i size, ShaderInstance shader)
    {
        handle.RenderInRenderTarget(target, () =>
        {
            handle.UseShader(shader);
            handle.SetTransform(Matrix3x2.Identity);
            handle.DrawRect(new Box2(-1, -1, size.X + 1, size.Y + 1), Color.White);
        }, null);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _prefilter.Dispose();
        _downsample.Dispose();
        _upsample.Dispose();
        _composite.Dispose();
        base.DisposeBehavior();
    }
}
