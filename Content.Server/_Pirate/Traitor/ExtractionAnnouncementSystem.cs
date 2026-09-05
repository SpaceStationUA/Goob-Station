// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radio.EntitySystems;
using Content.Shared._Pirate.Traitor;
using Content.Shared.Chat;
using Content.Shared.Salvage.Fulton;

namespace Content.Server._Pirate.Traitor;

/// <summary>
/// Announces fulton deliveries over the radio channel of the beacon that received them.
/// </summary>
public sealed class ExtractionAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FultonedComponent, FultonedEvent>(OnFultoned);
        SubscribeLocalEvent<ExtractionAnnouncementComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnFultoned(Entity<FultonedComponent> ent, ref FultonedEvent args)
    {
        // Nothing arrived if the beacon got destroyed mid-flight, so there is nothing to announce.
        if (!args.Delivered || ent.Comp.Beacon is not { } beacon)
            return;

        if (!TryComp<ExtractionAnnouncementComponent>(beacon, out var comp))
            return;

        var message = Loc.GetString(comp.Message, ("name", ent.Owner));

        // The beacon is both the speaker and the transmitter, so a map-wide channel reaches everyone
        // on the beacon's map. Beacons are TelecomExempt so the broadcast survives the base losing power.
        _radio.SendRadioMessage(beacon, message, comp.Channel, beacon);
    }

    private void OnTransformSpeakerName(Entity<ExtractionAnnouncementComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.SenderName is { } sender)
            args.VoiceName = Loc.GetString(sender);
    }
}
