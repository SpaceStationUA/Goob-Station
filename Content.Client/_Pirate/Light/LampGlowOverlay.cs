// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.Light;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Pirate.Light;

// Ambient occlusion changes destination alpha, so draw afterward. Mask unlit areas to avoid a grey-fog cone.
public sealed class LampGlowOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> GlowShader = "PirateLampGlow";

    private const float ConeDarkOpacity = 0f;
    private const float PointDarkOpacity = 0.25f;
    private const float DarkLuminance = 0.02f;
    private const float BrightLuminance = 0.3f;

    // Local coordinates use +Y for the wall and -Y for the lamp's beam.
    private const float PointRadiusX = 0.6f;
    private const float PointRadiusY = 0.28f;

    // Cut off the Gaussian at 3 radii; a smaller box would leave a visible edge.
    private const float PointExtent = 3f;
    private static readonly Box2 PointBox = new(-PointRadiusX * PointExtent, -PointRadiusY * PointExtent,
        PointRadiusX * PointExtent, PointRadiusY * PointExtent);

    private const float EdgeFade = 0.15f;

    private static readonly Box2 ConeBox = new(-1.5f, -2.8f, 1.5f, 0.2f);
    private const float ConeLength = 2.8f;
    private const float ConeStartWidth = 0.35f;
    private const float ConeWidthPerTile = 0.42f;

    private const float PointAlpha = 0.55f;
    private const float ConeAlpha = 0.2f;

    // Sqrt energy keeps weak and bright lamps within a usable range.
    private const float MinEnergyFactor = 0.5f;
    private const float MaxEnergyFactor = 1.2f;

    private const int TexturePixelsPerTile = 32;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xforms;
    private readonly EntityQuery<PointLightComponent> _lightQuery;
    // Separate instances avoid queued draws observing later parameter changes.
    private readonly ShaderInstance _coneShader;
    private readonly ShaderInstance _pointShader;
    private readonly OwnedTexture _point;
    private readonly OwnedTexture _cone;
    private readonly HashSet<Entity<LampGlowComponent>> _lamps = new();
    private readonly List<(Matrix3x2 Matrix, LampGlowComponent Glow, Color Color, float Strength)> _visible = new();

    public float Intensity = 1f;
    public bool DrawCones = true;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    public LampGlowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _lookup = _entMan.System<EntityLookupSystem>();
        _xforms = _entMan.System<SharedTransformSystem>();
        _lightQuery = _entMan.GetEntityQuery<PointLightComponent>();
        _coneShader = MakeShader(ConeDarkOpacity);
        _pointShader = MakeShader(PointDarkOpacity);
        _point = MakeTexture("pirate-lamp-glow-point", PointBox, PointShape);
        _cone = MakeTexture("pirate-lamp-glow-cone", ConeBox, ConeShape);

        ZIndex = (int) DrawDepth.Effects;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Intensity > 0f && args.MapId != MapId.Nullspace;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _lamps.Clear();
        // Use a spatial query because the client retains all seen entities; expand bounds for off-screen cones.
        var margin = MathF.Max(-ConeBox.Bottom, ConeBox.Right) * 2f;
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB.Enlarged(margin), _lamps,
            LookupFlags.Static | LookupFlags.Approximate);
        if (_lamps.Count == 0)
            return;

        _visible.Clear();
        foreach (var (uid, glow) in _lamps)
        {
            if (!glow.Enabled || !_lightQuery.TryComp(uid, out var light) || !light.Enabled)
                continue;

            var strength = Intensity * glow.Strength *
                           Math.Clamp(MathF.Sqrt(MathF.Max(light.Energy, 0f)), MinEnergyFactor, MaxEnergyFactor);
            if (strength <= 0f)
                continue;

            // Copy the color here; the render sandbox allows reading engine data but not invoking methods on it.
            _visible.Add((_xforms.GetWorldMatrix(uid), glow, light.Color, strength));
        }

        var handle = args.WorldHandle;
        // Draw against the original screen copy so overlapping lamps do not change each other's fade.
        if (DrawCones)
        {
            _coneShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            handle.UseShader(_coneShader);
            foreach (var (matrix, glow, color, strength) in _visible)
            {
                if (!glow.Cone)
                    continue;

                handle.SetTransform(matrix);
                handle.DrawTextureRect(_cone, Place(ConeBox, glow),
                    color.WithAlpha(Math.Clamp(ConeAlpha * strength, 0f, 1f)));
            }
        }

        _pointShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_pointShader);
        foreach (var (matrix, glow, color, strength) in _visible)
        {
            handle.SetTransform(matrix);
            handle.DrawTextureRect(_point, Place(PointBox, glow),
                color.WithAlpha(Math.Clamp(PointAlpha * strength, 0f, 1f)));
        }

        _visible.Clear();
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private ShaderInstance MakeShader(float darkOpacity)
    {
        var shader = _proto.Index(GlowShader).InstanceUnique();
        shader.SetParameter("DARK_OPACITY", darkOpacity);
        shader.SetParameter("DARK_LUMINANCE", DarkLuminance);
        shader.SetParameter("BRIGHT_LUMINANCE", BrightLuminance);
        return shader;
    }

    private static Box2 Place(Box2 shape, LampGlowComponent glow)
    {
        return new Box2(shape.BottomLeft * glow.Size + glow.Offset, shape.TopRight * glow.Size + glow.Offset);
    }

    private static float PointShape(Vector2 p)
    {
        var x = p.X / PointRadiusX;
        var y = p.Y / PointRadiusY;
        return MathF.Exp(-(x * x + y * y));
    }

    private static float ConeShape(Vector2 p)
    {
        var d = -p.Y;
        var along = SmoothStep(-0.2f, 0.3f, d) * MathF.Pow(Math.Clamp(1f - d / ConeLength, 0f, 1f), 1.5f);
        var half = ConeStartWidth + ConeWidthPerTile * MathF.Max(d, 0f);
        var across = 1f - SmoothStep(0.35f * half, half, MathF.Abs(p.X));
        return along * across;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private OwnedTexture MakeTexture(string name, Box2 box, Func<Vector2, float> shape)
    {
        var width = (int) MathF.Round(box.Width * TexturePixelsPerTile);
        var height = (int) MathF.Round(box.Height * TexturePixelsPerTile);
        var image = new Image<Rgba32>(width, height);
        for (var row = 0; row < height; row++)
        {
            // Image rows start at the top; DrawTextureRect places that row at +Y.
            var y = box.Top - (row + 0.5f) / height * box.Height;
            for (var col = 0; col < width; col++)
            {
                var x = box.Left + (col + 0.5f) / width * box.Width;
                // Fade texture edges smoothly; otherwise the texture bounds show as a box.
                var u = MathF.Min(col + 0.5f, width - col - 0.5f) / (width * 0.5f);
                var v = MathF.Min(row + 0.5f, height - row - 0.5f) / (height * 0.5f);
                var window = SmoothStep(0f, EdgeFade, u) * SmoothStep(0f, EdgeFade, v);
                var alpha = shape(new Vector2(x, y)) * window;
                image[col, row] = new Rgba32(255, 255, 255, (byte) MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f));
            }
        }

        return _clyde.LoadTextureFromImage(image, name,
            new TextureLoadParameters { SampleParameters = new TextureSampleParameters { Filter = true } });
    }

    protected override void DisposeBehavior()
    {
        _coneShader.Dispose();
        _pointShader.Dispose();
        _point.Dispose();
        _cone.Dispose();
        base.DisposeBehavior();
    }
}
