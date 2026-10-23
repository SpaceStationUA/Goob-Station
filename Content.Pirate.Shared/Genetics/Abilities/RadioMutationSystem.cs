// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Radio.Components;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Abilities;

public sealed class RadioMutationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<RadioMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<RadioMutationComponent> ent, ref MutationAddedEvent args)
    {
        var mob = args.Target;
        ent.Comp.CreatedReceiver = !HasComp<IntrinsicRadioReceiverComponent>(mob);
        ent.Comp.CreatedActive = !HasComp<ActiveRadioComponent>(mob);
        ent.Comp.CreatedTransmitter = !HasComp<IntrinsicRadioTransmitterComponent>(mob);
        EnsureComp<IntrinsicRadioReceiverComponent>(mob);
        var active = EnsureComp<ActiveRadioComponent>(mob);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        // Pirate: a new local transmitter defaults to Common. Grant and track only mutation channels.
        if (ent.Comp.CreatedTransmitter)
            transmitter.Channels.Clear();
        foreach (var channel in ent.Comp.Channels)
        {
            if (active.Channels.Add(channel))
                ent.Comp.AddedActive.Add(channel);
            if (transmitter.Channels.Add(channel))
                ent.Comp.AddedTransmitters.Add(channel);
        }
        Dirty(ent);
    }

    private void OnRemoved(Entity<RadioMutationComponent> ent, ref MutationRemovedEvent args)
    {
        var mob = args.Target;
        if (TryComp<ActiveRadioComponent>(mob, out var active))
        {
            foreach (var channel in ent.Comp.AddedActive)
                active.Channels.Remove(channel);

            if (active.Channels.Count == 0 && ent.Comp.CreatedActive)
                RemComp(mob, active);
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(mob, out var transmitter))
        {
            foreach (var channel in ent.Comp.AddedTransmitters)
                transmitter.Channels.Remove(channel);

            if (transmitter.Channels.Count == 0 && ent.Comp.CreatedTransmitter)
                RemComp(mob, transmitter);
        }

        if (ent.Comp.CreatedReceiver && (active == null || active.Channels.Count == 0))
            RemComp<IntrinsicRadioReceiverComponent>(mob);

        ent.Comp.AddedActive.Clear();
        ent.Comp.AddedTransmitters.Clear();
        ent.Comp.CreatedReceiver = false;
        ent.Comp.CreatedActive = false;
        ent.Comp.CreatedTransmitter = false;
        Dirty(ent);
    }
}
