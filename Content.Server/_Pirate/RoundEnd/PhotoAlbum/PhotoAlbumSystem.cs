// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Linq;
using Content.Server._Pirate.Photo;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Pirate.RoundEnd;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;
public sealed class PhotoAlbumSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    private readonly HashSet<EntityUid> _unsignedAutoSignAlbums = new();
    private readonly Dictionary<Guid, byte[]> _roundEndImageData = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeNetworkEvent<PhotoAlbumImageRequestEvent>(OnPhotoAlbumImageRequest);

        SubscribeLocalEvent<AutoSignPhotoAlbumComponent, ComponentStartup>(OnAutoSignStartup);
        SubscribeLocalEvent<AutoSignPhotoAlbumComponent, ComponentShutdown>(OnAutoSignShutdown);

        SubscribeLocalEvent<PhotoAlbumComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<PhotoAlbumComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PhotoAlbumComponent, ComponentStartup>(OnPhotoAlbumStartup);
        SubscribeLocalEvent<PhotoAlbumComponent, ComponentShutdown>(OnPhotoAlbumShutdown);
        SubscribeLocalEvent<PhotoAlbumComponent, EntInsertedIntoContainerMessage>(OnPhotoAlbumInserted);
        SubscribeLocalEvent<PhotoAlbumComponent, EntRemovedFromContainerMessage>(OnPhotoAlbumRemoved);
    }

    private void OnPhotoAlbumImageRequest(PhotoAlbumImageRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_roundEndImageData.TryGetValue(ev.ImageId, out var imageData))
        {
            Log.Warning($"Round-end photo image cache miss for ImageId {ev.ImageId} requested by session {args.SenderSession}.");
        }

        RaiseNetworkEvent(new PhotoAlbumImageResponseEvent(ev.ImageId, imageData), args.SenderSession);
    }

    private void OnPhotoAlbumStartup(Entity<PhotoAlbumComponent> entity, ref ComponentStartup args)
    {
        RefreshUnsignedAutoSignTracking(entity.Owner);

        if (entity.Comp.IsSigned)
            UpdateSignedAlbumName(entity);
    }

    private void OnPhotoAlbumShutdown(Entity<PhotoAlbumComponent> entity, ref ComponentShutdown args)
    {
        _unsignedAutoSignAlbums.Remove(entity.Owner);
    }

    private void OnAutoSignStartup(EntityUid uid, AutoSignPhotoAlbumComponent component, ref ComponentStartup args)
    {
        RefreshUnsignedAutoSignTracking(uid);
    }

    private void OnAutoSignShutdown(EntityUid uid, AutoSignPhotoAlbumComponent component, ref ComponentShutdown args)
    {
        _unsignedAutoSignAlbums.Remove(uid);
    }

    private void OnPhotoAlbumInserted(EntityUid uid, PhotoAlbumComponent component, EntInsertedIntoContainerMessage args)
    {
        RefreshUnsignedAutoSignTracking(uid, component);
    }

    private void OnPhotoAlbumRemoved(EntityUid uid, PhotoAlbumComponent component, EntRemovedFromContainerMessage args)
    {
        RefreshUnsignedAutoSignTracking(uid, component);
    }

    private void RefreshUnsignedAutoSignTracking(EntityUid uid, PhotoAlbumComponent? photoAlbum = null)
    {
        if (!Resolve(uid, ref photoAlbum, false) ||
            photoAlbum.IsSigned ||
            !HasComp<AutoSignPhotoAlbumComponent>(uid) ||
            !SupportsSigning(uid))
        {
            _unsignedAutoSignAlbums.Remove(uid);
            return;
        }

        _unsignedAutoSignAlbums.Add(uid);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (_unsignedAutoSignAlbums.Count == 0)
            return;

        var snapshot = new List<EntityUid>(_unsignedAutoSignAlbums);
        foreach (var uid in snapshot)
        {
            if (!TryComp<PhotoAlbumComponent>(uid, out var photoAlbum) ||
                !HasComp<AutoSignPhotoAlbumComponent>(uid) ||
                photoAlbum.IsSigned ||
                !SupportsSigning(uid))
            {
                _unsignedAutoSignAlbums.Remove(uid);
                continue;
            }

            if (!IsOwnedBy(uid, ev.Mob))
                continue;

            SignPhotoAlbum((uid, photoAlbum), ev.Mob, ev.Player.Data.UserName, usePossessiveSignerName: true);
        }
    }

    private bool IsOwnedBy(EntityUid uid, EntityUid owner)
    {
        var current = uid;
        var depth = 0;

        while (depth < 64 && _container.TryGetContainingContainer(current, out var container))
        {
            if (container.Owner == owner)
                return true;

            current = container.Owner;
            depth++;
        }

        return false;
    }

    private void OnGetVerbs(Entity<PhotoAlbumComponent> entity, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (args.Using is not { } pen || !_tags.HasTag(pen, "Write"))
            return;

        var user = args.User;

        if (!entity.Comp.IsSigned && SupportsSigning(entity.Owner))
        {
            var signVerb = new Verb
            {
                Text = Loc.GetString("photoalbum-sign-verb"),
                Act = () => VerbSignPhotoAlbum(entity, user)
            };
            args.Verbs.Add(signVerb);
        }

        if (TryComp<PersistentPhotoAlbumComponent>(entity, out var persistent) && persistent.SupportsPrivacy)
        {
            var privacyVerb = new Verb
            {
                Text = persistent.EffectiveIsPublic
                    ? Loc.GetString("photoalbum-make-private-verb")
                    : Loc.GetString("photoalbum-make-public-verb"),
                Icon = persistent.EffectiveIsPublic
                    ? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png"))
                    : new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png")),
                Act = () => VerbToggleAlbumPrivacy(entity, persistent, user)
            };
            args.Verbs.Add(privacyVerb);
        }
    }

    private void OnExamine(Entity<PhotoAlbumComponent> entity, ref ExaminedEvent args)
    {
        if (TryComp<PersistentPhotoAlbumComponent>(entity, out var persistent))
        {
            args.PushMarkup(Loc.GetString("photoalbum-persistent-examine"));

            if (persistent.SupportsPrivacy && !persistent.EffectiveIsPublic)
                args.PushMarkup(Loc.GetString("photoalbum-private-examine"));
        }

        if (entity.Comp.IsSigned && SupportsSigning(entity.Owner))
            args.PushMarkup(Loc.GetString("photoalbum-signed-examine"));
    }

    private void VerbSignPhotoAlbum(Entity<PhotoAlbumComponent> entity, EntityUid user)
    {
        if (!SupportsSigning(entity.Owner))
            return;

        string? username = null;
        if (_player.TryGetSessionByEntity(user, out var session))
            username = session.Data.UserName;

        SignPhotoAlbum(entity, user, username, usePossessiveSignerName: true);

        _popup.PopupEntity(Loc.GetString("photoalbum-signed", ("user", user)), entity);
        _audio.PlayPvs(entity.Comp.SignSound, entity);
    }

    private void SignPhotoAlbum(Entity<PhotoAlbumComponent> entity, EntityUid signer, string? signerUsername, bool usePossessiveSignerName)
    {
        entity.Comp.IsSigned = true;
        entity.Comp.SignerUid = signer;
        entity.Comp.SignerUsername = signerUsername;
        entity.Comp.SignerName = MetaData(signer).EntityName;
        entity.Comp.UsePossessiveSignerName = usePossessiveSignerName;

        _unsignedAutoSignAlbums.Remove(entity.Owner);
        UpdateSignedAlbumName(entity);
    }

    private void UpdateSignedAlbumName(Entity<PhotoAlbumComponent> entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Comp.SignerName))
            return;

        var albumName = entity.Comp.UsePossessiveSignerName
            ? Loc.GetString("photoalbum-signed-name-possessive", ("name", entity.Comp.SignerName))
            : Loc.GetString("photoalbum-signed-name-title", ("name", entity.Comp.SignerName));

        _metaData.SetEntityName(entity, albumName);
    }

    private bool SupportsSigning(EntityUid uid)
    {
        return !TryComp<PersistentPhotoAlbumComponent>(uid, out var persistent) || persistent.SupportsSigning;
    }

    private void VerbToggleAlbumPrivacy(
        Entity<PhotoAlbumComponent> entity,
        PersistentPhotoAlbumComponent persistent,
        EntityUid user)
    {
        if (!persistent.SupportsPrivacy)
            return;

        persistent.IsPublic = !persistent.EffectiveIsPublic;

        var popup = persistent.EffectiveIsPublic
            ? Loc.GetString("photoalbum-made-public", ("user", user))
            : Loc.GetString("photoalbum-made-private", ("user", user));
        _popup.PopupEntity(popup, entity);
        _audio.PlayPvs(entity.Comp.SignSound, entity);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
    {
        _roundEndImageData.Clear();

        List<AlbumData>? albums = new();
        var sortableAlbums = new List<(DateTime LatestActivity, int OriginalIndex, AlbumData Album)>();
        var query = EntityQueryEnumerator<PhotoAlbumComponent>();
        var albumIndex = 0;

        while (query.MoveNext(out var uid, out var photoAlbum)) // query all photoalbums and send photos them to players
        {
            if (TryComp<PersistentPhotoAlbumComponent>(uid, out var persistentAlbum) && !persistentAlbum.EffectiveIsPublic)
                continue;

            if (!_container.TryGetContainer(uid, photoAlbum.ContainerId, out var container))
                continue;

            List<AlbumImageData> photos = new();
            var sortablePhotos = new List<(int OriginalIndex, PhotoCardComponent PhotoCard)>();

            string? authorCKey = default;
            string? authorName = default;

            for (var i = 0; i < container.ContainedEntities.Count; i++)
            {
                var item = container.ContainedEntities[i];
                if (!TryComp<PhotoCardComponent>(item, out var photoCard))
                    continue;

                if (photoCard.ImageData is null)
                    continue;

                sortablePhotos.Add((i, photoCard));
            }

            var sortedPhotos = sortablePhotos
                .OrderByDescending(entry => GetPhotoSortTimestamp(entry.PhotoCard))
                .ThenBy(entry => entry.OriginalIndex)
                .ToList();

            foreach (var (_, photoCard) in sortedPhotos)
            {
                var imageId = Guid.NewGuid();
                _roundEndImageData[imageId] = photoCard.ImageData!;
                photos.Add(new AlbumImageData(imageId, photoCard.PreviewData, photoCard.CustomName));
            }

            if (photos.Count == 0)
                continue;

            if (photoAlbum.IsSigned && SupportsSigning(uid))
            {
                if (photoAlbum.SignerUid is not null && Exists(photoAlbum.SignerUid))
                    authorName = MetaData(photoAlbum.SignerUid.Value).EntityName;
                else
                    authorName = photoAlbum.SignerName;

                authorCKey = photoAlbum.SignerUsername;
            }

            var latestActivity = GetPhotoSortTimestamp(sortedPhotos[0].PhotoCard);
            var title = MetaData(uid).EntityName;
            sortableAlbums.Add((latestActivity, albumIndex++, new AlbumData(photos, title, authorCKey, authorName)));
        }

        foreach (var (_, _, album) in sortableAlbums
                     .OrderByDescending(entry => entry.LatestActivity)
                     .ThenBy(entry => entry.OriginalIndex))
        {
            albums.Add(album);
        }

        if (albums.Count > 0)
            RaiseNetworkEvent(new PhotoAlbumEvent(albums));
    }

    private static DateTime GetPhotoSortTimestamp(PhotoCardComponent photoCard)
    {
        var createdAt = photoCard.CreatedAt ?? DateTime.MinValue;
        var updatedAt = photoCard.UpdatedAt ?? DateTime.MinValue;
        return createdAt >= updatedAt ? createdAt : updatedAt;
    }
}


