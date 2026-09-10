// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;

namespace Content.Server._Pirate.Announcements;

/// <summary>Announces configured cryostorage departures over radio.</summary>
public sealed class CryostorageRadioAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryostorageRadioAnnouncementComponent, CryostorageAnnounceAttemptEvent>(OnAnnounceAttempt);
        SubscribeLocalEvent<CryostorageRadioAnnouncementComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnAnnounceAttempt(Entity<CryostorageRadioAnnouncementComponent> ent, ref CryostorageAnnounceAttemptEvent args)
    {
        if (args.Handled)
            return;

        var job = Loc.GetString(ent.Comp.UnknownJob);
        if (_idCard.TryFindIdCard(args.Stored, out var idCard) && idCard.Comp.LocalizedJobTitle is { } title)
            job = title;

        var message = Loc.GetString(ent.Comp.Message, ("character", args.StoredName), ("job", job));

        _radio.SendRadioMessage(ent.Owner, message, ent.Comp.Channel, ent.Owner);

        args.Handled = true;
    }

    private void OnTransformSpeakerName(Entity<CryostorageRadioAnnouncementComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.SenderName is { } sender)
            args.VoiceName = Loc.GetString(sender);
    }
}
