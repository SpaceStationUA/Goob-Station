// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Client.Sprite;
using Content.Client.Viewport;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth.Components;
using Robust.Client.ComponentTrees;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System;
using System.Numerics;

namespace Content.Client._Pirate.Photo;

/// <summary>
/// Captures mobs that are visible inside the camera viewport bounds.
/// </summary>
public sealed class PhotoCaptureEntityDetectorSystem : EntitySystem
{
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    private EntityQuery<FadingSpriteComponent> _fadingQuery = default!;
    private EntityQuery<StealthComponent> _stealthQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _fadingQuery = GetEntityQuery<FadingSpriteComponent>();
        _stealthQuery = GetEntityQuery<StealthComponent>();
    }

    public IReadOnlyList<NetEntity> CaptureVisibleEntities(ScalingViewport viewport, int maxEntities = 256)
    {
        var eye = viewport.Eye;
        if (eye == null)
            return Array.Empty<NetEntity>();

        if (maxEntities <= 0)
            return Array.Empty<NetEntity>();

        if (eye.Position.MapId == MapId.Nullspace)
            return Array.Empty<NetEntity>();

        var viewportSize = viewport.ViewportSize;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            return Array.Empty<NetEntity>();

        if (!TryGetViewportWorldBounds(viewport, out var worldBounds))
            return Array.Empty<NetEntity>();

        var visibleSprites = _spriteTree.QueryAabb(eye.Position.MapId, worldBounds);
        var unique = new HashSet<NetEntity>(maxEntities);

        foreach (var sprite in visibleSprites)
        {
            if (!sprite.Component.Visible)
                continue;

            var entity = sprite.Uid;
            if (!_mobStateQuery.HasComp(entity))
                continue;

            if (_fadingQuery.HasComp(entity))
                continue;

            if (_stealthQuery.TryGetComponent(entity, out var stealth) && stealth.Enabled)
                continue;

            if (!EntityManager.TryGetNetEntity(entity, out var net) || net == null)
                continue;

            unique.Add(net.Value);
            if (unique.Count >= maxEntities)
                break;
        }

        return new List<NetEntity>(unique);
    }

    private static bool TryGetViewportWorldBounds(ScalingViewport viewport, out Box2Rotated bounds)
    {
        var localToScreen = viewport.GetLocalToScreenMatrix();
        var viewportLocalSize = (Vector2) (viewport.ViewportSize * viewport.CurrentRenderScale);
        var screenTopLeft = Vector2.Transform(Vector2.Zero, localToScreen);
        var screenBottomRight = Vector2.Transform(viewportLocalSize, localToScreen);
        var screenBounds = new Box2(Vector2.Min(screenTopLeft, screenBottomRight), Vector2.Max(screenTopLeft, screenBottomRight));
        var screenBox = new Box2Rotated(screenBounds, Angle.Zero);

        if (!Matrix3x2.Invert(viewport.GetWorldToScreenMatrix(), out var screenToWorld))
        {
            bounds = default;
            return false;
        }

        bounds = screenToWorld.TransformBounds(screenBox);
        return true;
    }
}


