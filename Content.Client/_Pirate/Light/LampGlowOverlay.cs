// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.Light;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
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

    // Light map values, before floor colour; dim lamps and unlit or hidden areas show no glow.
    private const float DarkOpacity = 0f;
    private const float DarkLuminance = 0.1f;
    private const float BrightLuminance = 0.6f;

    // Local coordinates use +Y for the wall and -Y for the lamp's beam.
    private const float PointRadiusX = 0.6f;
    private const float PointRadiusY = 0.28f;

    // Cut off the Gaussian at 3 radii; a smaller box would leave a visible edge.
    private const float PointExtent = 3f;
    private static readonly Box2 PointBox = new(-PointRadiusX * PointExtent, -PointRadiusY * PointExtent,
        PointRadiusX * PointExtent, PointRadiusY * PointExtent);

    private const float EdgeFade = 0.15f;

    // The texture edge fade hides the Gaussian cutoff at two radii.
    private const float FanRadius = 1.1f;
    private const float FanAngleExponent = 0.6f;
    private static readonly Box2 ConeBox = new(-FanRadius * 2f, -FanRadius * 2f, FanRadius * 2f, 0.3f);

    private const float PointAlpha = 0.55f;
    private const float ConeAlpha = 0.14f;

    // Sqrt energy keeps bright lamps within a usable range; weak lamps glow less.
    private const float MinEnergyFactor = 0.2f;
    private const float MaxEnergyFactor = 1.2f;

    private const int TexturePixelsPerTile = 32;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xforms;
    private readonly OccluderSystem _occluders;
    private readonly EntityQuery<PointLightComponent> _lightQuery;
    private readonly ShaderInstance _shader;
    private readonly OwnedTexture _point;
    private readonly OwnedTexture _cone;
    private readonly HashSet<Entity<LampGlowComponent>> _lamps = new();
    private readonly List<(Matrix3x2 Matrix, LampGlowComponent Glow, Color Color, float Strength)> _visible = new();

    public float Intensity = 1f;
    public bool DrawCones = true;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public LampGlowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _lookup = _entMan.System<EntityLookupSystem>();
        _xforms = _entMan.System<SharedTransformSystem>();
        _occluders = _entMan.System<OccluderSystem>();
        _lightQuery = _entMan.GetEntityQuery<PointLightComponent>();
        _shader = _proto.Index(GlowShader).InstanceUnique();
        _shader.SetParameter("DARK_OPACITY", DarkOpacity);
        _shader.SetParameter("DARK_LUMINANCE", DarkLuminance);
        _shader.SetParameter("BRIGHT_LUMINANCE", BrightLuminance);
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
        _lamps.Clear();
        // Use a spatial query because the client retains all seen entities; expand bounds for off-screen cones.
        var margin = MathF.Max(-ConeBox.Bottom, ConeBox.Right) * 2f;
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB.Enlarged(margin), _lamps,
            LookupFlags.Static | LookupFlags.Approximate);
        if (_lamps.Count == 0)
            return;

        // Observers without FOV, and views of another map, see every lamp.
        var eye = args.Viewport.Eye;
        var checkSight = eye is { DrawFov: true } && eye.Position.MapId == args.MapId;
        var eyePosition = eye?.Position.Position ?? Vector2.Zero;

        _visible.Clear();
        foreach (var (uid, glow) in _lamps)
        {
            if (!glow.Enabled || !_lightQuery.TryComp(uid, out var light) || !light.Enabled)
                continue;

            var strength = Intensity * glow.Strength *
                           Math.Clamp(MathF.Sqrt(MathF.Max(light.Energy, 0f)), MinEnergyFactor, MaxEnergyFactor);
            if (strength <= 0f)
                continue;

            var matrix = _xforms.GetWorldMatrix(uid);
            // FOV leaves walls visible and lit from both sides. Test the floor tile before each lamp.
            if (checkSight && !InSight(args.MapId, eyePosition, matrix.Translation))
                continue;

            // Copy the color here; the render sandbox allows reading engine data but not invoking methods on it.
            _visible.Add((matrix, glow, light.Color, strength));
        }

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        if (DrawCones)
        {
            foreach (var (matrix, glow, color, strength) in _visible)
            {
                if (!glow.Cone)
                    continue;

                handle.SetTransform(matrix);
                handle.DrawTextureRect(_cone, Place(ConeBox, glow),
                    color.WithAlpha(Math.Clamp(ConeAlpha * strength, 0f, 1f)));
            }
        }

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

    private bool InSight(MapId map, Vector2 from, Vector2 to)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length < 0.01f)
            return true;

        return _occluders.IntersectRay(map, new Ray(from, direction / length), length) == null;
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
        var r = MathF.Sqrt(p.X * p.X + d * d) + 1e-6f;
        var angle = MathF.Pow(Math.Clamp(d / r, 0f, 1f), FanAngleExponent);
        var radial = MathF.Exp(-(r / FanRadius) * (r / FanRadius));
        return SmoothStep(-0.2f, 0.4f, d) * angle * radial;
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
        using var image = new Image<Rgba32>(width, height);
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
        _shader.Dispose();
        _point.Dispose();
        _cone.Dispose();
        base.DisposeBehavior();
    }
}
