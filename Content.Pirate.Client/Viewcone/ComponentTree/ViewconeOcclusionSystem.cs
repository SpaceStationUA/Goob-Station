// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using System.Numerics;
using Content.Pirate.Shared.Viewcone.Components;
using Robust.Client.GameObjects;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Pirate.Client.Viewcone.ComponentTree;

/// <summary>
/// Handles gathering sprites to modify alpha in the viewcone overlays
/// </summary>
public sealed partial class ViewconeOcclusionSystem : ComponentTreeSystem<ViewconeOccludableTreeComponent, ViewconeOccludableComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    protected override bool DoFrameUpdate => true;
    protected override bool DoTickUpdate => false;
    protected override bool Recursive => false;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();
    }

    protected override Box2 ExtractAabb(in ComponentTreeEntry<ViewconeOccludableComponent> entry, Vector2 pos, Angle rot)
    {
        return _sprite.CalculateBounds((entry.Uid, _spriteQuery.Comp(entry.Uid)), pos, rot, default).CalcBoundingBox();
    }
}
