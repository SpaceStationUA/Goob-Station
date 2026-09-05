// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DragDrop;

namespace Content.Shared._Pirate.Buckle;

public sealed class BuckleableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BuckleableComponent, CanDragEvent>(OnCanDrag);
    }

    private static void OnCanDrag(Entity<BuckleableComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }
}
