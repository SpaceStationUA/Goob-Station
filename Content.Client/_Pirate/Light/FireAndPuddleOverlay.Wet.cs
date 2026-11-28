// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Light;

public sealed partial class FireAndPuddleOverlay
{
    private static readonly ProtoId<ShaderPrototype> _wetMaskShader = "PiratePuddleMask";

    /// <summary>
    /// Resolution of the wet floor mask relative to the viewport.
    /// </summary>
    private const float WetScale = 0.25f;

    private ShaderInstance? _wetSpriteShaderInst;
    private readonly HashSet<Entity<PuddleComponent>> _puddles = [];
    private readonly List<(Entity<SpriteComponent> Sprite, Vector2 Position, Angle Rotation)> _wetSprites = [];

    /// <summary>
    /// Draws puddle sprites into a soft mask, its alpha says how glossy the floor is.
    /// Returns null when nothing in view is glossy.
    /// </summary>
    private Texture DrawWet(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * WetScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * WetScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Wet?.Size != size)
        {
            res.Wet?.Dispose();
            res.Wet = _clyde.CreateRenderTarget(size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
                _smoothSampling,
                name: "wet-floor");
        }

        if (_wetSpriteShaderInst == null)
        {
            _wetSpriteShaderInst = _proto.Index(_wetMaskShader).InstanceUnique();
            _wetSpriteShaderInst.SetParameter("softness", 0.02f);
            _wetSpriteShaderInst.SetParameter("fromTexture", 1f);
        }

        var matrix = res.Wet.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var handle = args.WorldHandle;
        var eyeRotation = viewport.Eye!.Rotation;

        handle.RenderInRenderTarget(res.Wet,
            () =>
            {
                var spriteHandle = new TransformedWorldHandle(handle, matrix);
                foreach (var (sprite, position, rotation) in _wetSprites)
                {
                    spriteHandle.UseShader(_wetSpriteShaderInst);
                    _sprite.RenderSprite(sprite, spriteHandle, eyeRotation, rotation, position);
                }
            }, Color.Transparent);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        _wetSprites.Clear();
        return res.Wet.Texture;
    }

    private bool CollectWet(MapId mapId, Box2 worldAABB, Angle eyeRotation)
    {
        _wetSprites.Clear();

        _puddles.Clear();
        _lookup.GetEntitiesIntersecting(mapId, worldAABB.Enlarged(1f), _puddles,
            LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Sensors | LookupFlags.Approximate);
        foreach (var puddle in _puddles)
        {
            if (!_entMan.TryGetComponent<SpriteComponent>(puddle.Owner, out var sprite) || !sprite.Visible || sprite.ContainerOccluded)
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(puddle.Owner);
            _wetSprites.Add(((puddle.Owner, sprite), position, rotation));
            IncludeBounds(_sprite.CalculateBounds((puddle.Owner, sprite), position, rotation, eyeRotation).CalcBoundingBox());
        }

        _puddles.Clear();
        return _wetSprites.Count > 0;
    }
}
