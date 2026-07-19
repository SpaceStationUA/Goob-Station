// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ShipShields;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Physics;
using Robust.Shared.Prototypes;
using System.Runtime.InteropServices;

namespace Content.Client._Pirate.ShipShields;

public sealed class ShipShieldOverlay : Overlay
{
    private readonly IResourceCache _resourceCache;
    private readonly IEntityManager _entManager;
    private readonly FixtureSystem _fixture;
    private readonly SharedPhysicsSystem _physics;
    private readonly ShaderInstance _unshadedShader;
    private readonly List<DrawVertexUV2D> _verts = new(128);
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ShipShieldOverlay(IEntityManager entityManager, IPrototypeManager prototypeManager, IResourceCache resourceCache)
    {
        _resourceCache = resourceCache;
        _entManager = entityManager;
        _fixture = _entManager.EntitySysManager.GetEntitySystem<FixtureSystem>();
        _physics = _entManager.EntitySysManager.GetEntitySystem<Robust.Client.Physics.PhysicsSystem>();

        _unshadedShader = prototypeManager.Index<ShaderPrototype>("unshaded").Instance();

        ZIndex = 8;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        handle.UseShader(_unshadedShader);

        var enumerator = _entManager.AllEntityQueryEnumerator<ShipShieldVisualsComponent, FixturesComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var visuals, out var fixtures, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var fixture = _fixture.GetFixtureOrNull(uid, "shield", fixtures);

            if (fixture is not { Shape: ChainShape chain })
                continue;

            var texture = _resourceCache.GetTexture("/Textures/_Pirate/ShipShields/shieldtex.png");

            DrawShield(handle, uid, chain, xform, texture, visuals.ShieldColor, _verts);
            _verts.Clear();
        }
    }

    private void DrawShield(
        DrawingHandleWorld handle,
        EntityUid uid,
        ChainShape chain,
        TransformComponent xform,
        Texture tex,
        Color color,
        List<DrawVertexUV2D> verts)
    {
        var localPos = xform.LocalPosition;

        var transform = _physics.GetPhysicsTransform(uid);

        for (int i = 1; i < chain.Count; i++)
        {
            var leftVertex = VertexToWorldPos(chain.Vertices[i - 1], transform);

            var rightVertex = VertexToWorldPos(chain.Vertices[i], transform);

            var leftCorner = Corner(localPos, leftVertex, transform);

            var rightCorner = Corner(localPos, rightVertex, transform);

            verts.Add(new DrawVertexUV2D(leftVertex, new Vector2(0, 1)));
            verts.Add(new DrawVertexUV2D(rightVertex, new Vector2(1, 1)));
            verts.Add(new DrawVertexUV2D(leftCorner, Vector2.Zero));

            verts.Add(new DrawVertexUV2D(rightVertex, new Vector2(1, 1)));
            verts.Add(new DrawVertexUV2D(leftCorner, Vector2.Zero));
            verts.Add(new DrawVertexUV2D(rightCorner, new Vector2(1, 0)));
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture: tex, CollectionsMarshal.AsSpan(verts), color);
    }

    private static Vector2 VertexToWorldPos(Vector2 vertexPos, Transform transform)
    {
        return Transform.Mul(transform, vertexPos);
    }

    private static Vector2 Corner(Vector2 localPos, Vector2 vertexPos, Transform transform, float radius = 1.3f)
    {
        var localXform = Transform.Mul(transform, localPos);
        var cornerPos = Vector2.Subtract(vertexPos, localXform);
        cornerPos.Normalize();
        cornerPos *= radius;

        return Vector2.Subtract(vertexPos, cornerPos);
    }
}
