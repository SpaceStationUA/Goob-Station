// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Chat;
using Content.Shared.Mind.Components;

namespace Content.Pirate.Shared.Genetics.Mind;

public sealed partial class MindMessagesSystem : EntitySystem
{
    [Dependency] private EntityQuery<MindMessagesComponent> _query = default!;

    [SubscribeLocalEvent]
    private void OnContainerSpoke(Entity<MindContainerComponent> ent, ref EntitySpokeEvent args)
    {
        // relay event to the mind, other systems can use it too
        if (ent.Comp.Mind is {} mind)
            AddMessage(EnsureComp<MindMessagesComponent>(mind), args.Message); // Pirate: capture without a global mind prototype change.
    }

    [SubscribeLocalEvent]
    private void OnSpoke(Entity<MindMessagesComponent> ent, ref EntitySpokeEvent args)
    {
        AddMessage(ent.Comp, args.Message);
    }

    public void AddMessage(MindMessagesComponent comp, string message)
    {
        comp.Messages[comp.Index] = message;
        comp.Index++;
        comp.Index %= comp.Messages.Length;
    }

    public MindMessagesComponent? GetMessages(EntityUid? mind)
        => mind is { } && _query.TryComp(mind, out var comp)
            ? comp
            : null;

    /// <summary>
    /// Get one of the last messages for a mind, with 0 being the oldest.
    /// </summary>
    public string GetMessage(MindMessagesComponent comp, int i)
        => comp.Messages[(comp.Index + i) % comp.Messages.Length];
}
