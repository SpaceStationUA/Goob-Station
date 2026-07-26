// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Pirate.Shared.Viewcone.Components;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Pirate.Client.Viewcone.ComponentTree;

[RegisterComponent]
public sealed partial class ViewconeOccludableTreeComponent : Component, IComponentTreeComponent<ViewconeOccludableComponent>
{
    public DynamicTree<ComponentTreeEntry<ViewconeOccludableComponent>> Tree { get; set; } = default!;
}
