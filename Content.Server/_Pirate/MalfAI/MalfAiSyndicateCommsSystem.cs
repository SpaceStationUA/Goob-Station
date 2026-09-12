// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.MalfAI;
using Content.Shared.Radio.Components;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// System that handles granting syndicate radio communications to malfunction AI
/// </summary>
public sealed class MalfAiSyndicateCommsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiSyndicateKeysUnlockedEvent>(OnSyndicateKeysUnlocked);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, ComponentStartup>(OnTransmitterStartup);
        SubscribeLocalEvent<ActiveRadioComponent, ComponentStartup>(OnReceiverStartup);
    }

    private void OnSyndicateKeysUnlocked(EntityUid uid, MalfAiMarkerComponent component, MalfAiSyndicateKeysUnlockedEvent args)
    {
        EnsureComp<MalfAiSyndicateKeysComponent>(uid);
        // Add or get the IntrinsicRadioTransmitterComponent for sending syndicate messages
        var transmitterComp = EnsureComp<IntrinsicRadioTransmitterComponent>(uid);
        transmitterComp.Channels.Add("Syndicate");

        // Add or get the ActiveRadioComponent for receiving syndicate messages
        var activeRadioComp = EnsureComp<ActiveRadioComponent>(uid);
        activeRadioComp.Channels.Add("Syndicate");

        // IntrinsicRadioTransmitterComponent and ActiveRadioComponent are server-only and don't need network synchronization
    }

    private void OnTransmitterStartup(Entity<IntrinsicRadioTransmitterComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<MalfAiSyndicateKeysComponent>(ent))
            ent.Comp.Channels.Add("Syndicate");
    }

    private void OnReceiverStartup(Entity<ActiveRadioComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<MalfAiSyndicateKeysComponent>(ent))
            ent.Comp.Channels.Add("Syndicate");
    }
}

[RegisterComponent]
public sealed partial class MalfAiSyndicateKeysComponent : Component;
