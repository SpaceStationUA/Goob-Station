// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Silicons.Borgs.Components;

namespace Content.Shared._Pirate.Silicons;

/// <summary>
/// Relays chassis insertion events from an MMI to its contained brain.
/// </summary>
public sealed class BorgBrainRelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MMIComponent, BorgBrainInsertedEvent>(OnInserted);
        SubscribeLocalEvent<MMIComponent, BorgBrainRemovedEvent>(OnRemoved);
    }

    private void OnInserted(Entity<MMIComponent> ent, ref BorgBrainInsertedEvent args)
    {
        if (ent.Comp.BrainSlot.Item is not { } brain)
            return;

        RaiseLocalEvent(brain, ref args);
    }

    private void OnRemoved(Entity<MMIComponent> ent, ref BorgBrainRemovedEvent args)
    {
        if (ent.Comp.BrainSlot.Item is not { } brain)
            return;

        RaiseLocalEvent(brain, ref args);
    }
}
