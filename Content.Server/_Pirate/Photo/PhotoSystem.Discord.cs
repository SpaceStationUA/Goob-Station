// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Server._Pirate.RoundEnd.PhotoAlbum;
using Content.Shared.GameTicking;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._Pirate.Photo;

public sealed partial class PhotoSystem
{
    [Dependency] private readonly GameTicker _photoTicker = default!;
    private sealed record Submission(EntityUid Card, string Author, string Caption);
    private readonly Dictionary<NetUserId, Submission> _discordPhotos = new();

    private void InitializeDiscordPhotos()
    {
        SubscribeLocalEvent<PhotoCardComponent, GetVerbsEvent<Verb>>(OnDiscordPhotoVerbs);
        SubscribeLocalEvent<ActorComponent, GetVerbsEvent<Verb>>(OnPhotographerVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _discordPhotos.Clear());
    }

    internal bool WithdrawDiscordPhoto(NetUserId owner)
    {
        return _photoTicker.RunLevel == GameRunLevel.InRound && _discordPhotos.Remove(owner);
    }

    private void OnPhotographerVerbs(Entity<ActorComponent> actor, ref GetVerbsEvent<Verb> args)
    {
        var owner = actor.Comp.PlayerSession.UserId;
        if (args.User != actor.Owner || !_discordPhotos.ContainsKey(owner) || _photoTicker.RunLevel != GameRunLevel.InRound)
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("pirate-shift-photo-withdraw"),
            Act = () =>
            {
                if (actor.Comp.PlayerSession.AttachedEntity == actor.Owner && WithdrawDiscordPhoto(owner))
                    _popup.PopupEntity(Loc.GetString("pirate-shift-photo-withdrawn"), actor.Owner, actor.Owner);
            },
        });
    }

    private void OnDiscordPhotoVerbs(Entity<PhotoCardComponent> photo, ref GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        if (_photoTicker.RunLevel != GameRunLevel.InRound || !args.CanAccess || !args.CanInteract || !TryComp<ActorComponent>(user, out var actor)
            || photo.Comp.Photographer != actor.PlayerSession.UserId || photo.Comp.CaptureRound != _photoTicker.RoundId)
            return;

        var owner = actor.PlayerSession.UserId;
        var selected = _discordPhotos.TryGetValue(owner, out var current) && current.Card == photo.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(selected ? "pirate-shift-photo-withdraw" : "pirate-shift-photo-select"),
            Act = () =>
            {
                if (!CanContinueCustomization(user, photo) || actor.PlayerSession.AttachedEntity != user)
                    return;
                if (selected)
                {
                    if (_discordPhotos.TryGetValue(owner, out var entry) && entry.Card == photo.Owner)
                        _discordPhotos.Remove(owner);
                    _popup.PopupEntity(Loc.GetString("pirate-shift-photo-withdrawn"), user, user);
                    return;
                }
                _quickDialog.OpenDialogPrefilled<string>(actor.PlayerSession,
                    Loc.GetString("pirate-shift-photo-dialog"), Loc.GetString("pirate-shift-photo-consent"), caption =>
                    {
                        if (_photoTicker.RunLevel != GameRunLevel.InRound || actor.PlayerSession.AttachedEntity != user || !CanContinueCustomization(user, photo)
                            || !TryComp<PhotoCardComponent>(photo, out var card) || card.Photographer != owner
                            || card.CaptureRound != _photoTicker.RoundId)
                            return;
                        if (InPrivateAlbum(photo))
                        {
                            _popup.PopupEntity(Loc.GetString("pirate-shift-photo-private"), user, user);
                            return;
                        }
                        _discordPhotos[owner] = new Submission(photo, MetaData(user).EntityName,
                            TrimToLength(caption, MaxCustomCaptionLength) ?? string.Empty);
                        _popup.PopupEntity(Loc.GetString("pirate-shift-photo-selected"), user, user);
                    }, initialValue: photo.Comp.Caption ?? photo.Comp.CustomName);
            },
        });
    }

    private bool InPrivateAlbum(EntityUid item)
    {
        while (TryComp<TransformComponent>(item, out var xform) && xform.ParentUid.Valid)
        {
            item = xform.ParentUid;
            if (TryComp<PersistentPhotoAlbumComponent>(item, out var album) && !album.EffectiveIsPublic)
                return true;
        }
        return false;
    }

    internal void WriteDiscordPhotos(MappingDataNode metadata)
    {
        var photos = new SequenceDataNode();
        foreach (var (owner, submission) in _discordPhotos)
        {
            if (!TryComp<PhotoCardComponent>(submission.Card, out var card) || card.ImageData == null
                || card.Photographer != owner || card.CaptureRound != _photoTicker.RoundId || InPrivateAlbum(submission.Card))
                continue;
            photos.Add(new MappingDataNode
            {
                ["author"] = new ValueDataNode(submission.Author),
                ["caption"] = new ValueDataNode(submission.Caption),
                ["png"] = new ValueDataNode(Convert.ToBase64String(card.ImageData)),
            });
        }
        if (photos.Count > 0)
            metadata["pirateShiftPhotos"] = photos;
    }
}
