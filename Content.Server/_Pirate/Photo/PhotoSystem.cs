// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Server.Hands.Systems;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Administration;
using Content.Shared._Pirate.Photo;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Content.Shared.Timing;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

namespace Content.Server._Pirate.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    private static readonly ProtoId<TagPrototype> CameraFilmTag = "CameraFilm";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";
    private readonly Dictionary<EntityUid, int> _openCameraCounts = new();

    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly MaterialStorageSystem _material = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!; // # Pirate: camera
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    //96 KB
    const int MAX_SIZE = 1024 * 96;
    // 16 KB
    const int MAX_PREVIEW_SIZE = 1024 * 16;
    const int MAX_WIDTH = 1024;
    const int MAX_HEIGHT = 1024;
    const int MAX_PIXELS = MAX_WIDTH * MAX_HEIGHT;
    const int MAX_PREVIEW_WIDTH = 256;
    const int MAX_PREVIEW_HEIGHT = 256;
    const int MAX_PREVIEW_PIXELS = MAX_PREVIEW_WIDTH * MAX_PREVIEW_HEIGHT;
    private const int PreviewSize = 8;
    private const int PreviewSampleSize = 32;
    private const int MaxCustomNameLength = 32;
    private const int MaxCustomDescriptionLength = 128;
    private const int MaxCustomCaptionLength = 256;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotoCameraComponent, AfterActivatableUIOpenEvent>(OnOpenCameraInterface);
        Subs.BuiEvents<PhotoCameraComponent>(PhotoCameraUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnCameraBoundUiClose);
            subs.Event<PhotoCameraTakeImageMessage>(OnTakeImageMessage);
        });
        SubscribeLocalEvent<PhotoCameraComponent, ComponentRemove>(OnCameraComponentRemove);
        SubscribeLocalEvent<PhotoCameraComponent, MaterialAmountChangedEvent>(OnPaperInserted);
        SubscribeLocalEvent<PhotoCameraComponent, InteractUsingEvent>(OnCameraInteractUsing);
        SubscribeLocalEvent<ActorComponent, EntityTerminatingEvent>(OnCameraUserTerminating);

        SubscribeLocalEvent<PhotoCardComponent, AfterActivatableUIOpenEvent>(OnOpenCardInterface);
        SubscribeLocalEvent<PhotoCardComponent, InteractUsingEvent>(OnPhotoCardInteractUsing);
    }

    private void OnOpenCameraInterface(EntityUid uid, PhotoCameraComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateCameraInterface(uid, component);
        CloseOtherOpenCameras(args.User, uid);

        if (!component.OpenUsers.Add(args.User))
            return;

        IncrementOpenCameraCount(args.User);
    }

    private void OnCameraBoundUiClose(EntityUid uid, PhotoCameraComponent component, BoundUIClosedEvent args)
    {
        if (!component.OpenUsers.Remove(args.Actor))
            return;

        DecrementOpenCameraCount(args.Actor);
    }

    private void OnCameraComponentRemove(EntityUid uid, PhotoCameraComponent component, ComponentRemove args)
    {
        foreach (var user in component.OpenUsers)
        {
            DecrementOpenCameraCount(user);
        }

        component.OpenUsers.Clear();
    }

    private void IncrementOpenCameraCount(EntityUid user)
    {
        if (_openCameraCounts.ContainsKey(user))
            return;

        _openCameraCounts[user] = 1;
        EnsureComp<PhotoCameraUserComponent>(user);
    }

    private void DecrementOpenCameraCount(EntityUid user)
    {
        if (!_openCameraCounts.ContainsKey(user))
            return;

        CleanupOpenCameraUserState(user);
    }

    private void CloseOtherOpenCameras(EntityUid user, EntityUid activeCamera)
    {
        var query = EntityQueryEnumerator<PhotoCameraComponent>();
        while (query.MoveNext(out var cameraUid, out var camera))
        {
            if (cameraUid == activeCamera)
                continue;

            if (!camera.OpenUsers.Remove(user))
                continue;

            _userInterface.CloseUi(cameraUid, PhotoCameraUiKey.Key, user);
            DecrementOpenCameraCount(user);
        }
    }

    private void OnCameraUserTerminating(EntityUid uid, ActorComponent component, ref EntityTerminatingEvent args)
    {
        CleanupOpenCameraUserState(uid);
    }

    private void CleanupOpenCameraUserState(EntityUid user)
    {
        _openCameraCounts.Remove(user);
        RemComp<PhotoCameraUserComponent>(user);
    }

    private void OnTakeImageMessage(EntityUid uid, PhotoCameraComponent component, PhotoCameraTakeImageMessage message)
    {
        if (!component.OpenUsers.Contains(message.Actor))
            return;

        if (_delay.IsDelayed(uid))
            return;

        if (!CanPrintPhoto(uid, component))
            return;

        if (!ValidatePngData(message.Data, MAX_SIZE, MAX_WIDTH, MAX_HEIGHT, MAX_PIXELS))
            return;

        byte[]? previewData = null;
        if (message.PreviewData is { Length: > 0 } preview &&
            ValidatePngData(preview, MAX_PREVIEW_SIZE, MAX_PREVIEW_WIDTH, MAX_PREVIEW_HEIGHT, MAX_PREVIEW_PIXELS))
        {
            previewData = preview;
        }

        var sanitizedZoom = SanitizeCaptureZoom(component, message.Zoom);
        var photoPosition = _transform.GetMapCoordinates(uid);

        if (TryTakeImage(uid, component, message.Actor, message.Data, previewData, message.CapturedEntities, sanitizedZoom))
            RaiseLocalEvent(new PhotoCameraTakeImageEvent(uid, message.Actor, photoPosition, sanitizedZoom));
    }

    private void UpdateCameraInterface(EntityUid uid, PhotoCameraComponent component)
    {
        var hasPaper = CanPrintPhoto(uid, component);

        var state = new PhotoCameraUiState(GetNetEntity(uid), hasPaper);
        _userInterface.SetUiState(uid, PhotoCameraUiKey.Key, state);
    }

    private void OnPaperInserted(EntityUid uid, PhotoCameraComponent component, MaterialAmountChangedEvent args)
    {
        if (TryComp<MaterialStorageComponent>(uid, out var storage))
            Dirty(uid, storage);

        UpdateCameraInterface(uid, component);
    }

    private void OnCameraInteractUsing(EntityUid uid, PhotoCameraComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, CameraFilmTag))
            return;

        args.Handled = true;

        if (!IsValidCardCost(uid, component))
            return;

        var amount = _material.GetMaterialAmount(uid, component.CardMaterial);
        if (amount >= component.CardCost)
        {
            _popup.PopupEntity(Loc.GetString("photo-camera-film-not-empty"), uid, args.User);
            return;
        }

        if (!TryComp<MaterialStorageComponent>(uid, out var storage))
            return;

        if (!_material.TryInsertMaterialEntity(args.User, args.Used, uid, storage))
        {
            _popup.PopupEntity(Loc.GetString("photo-camera-film-cannot-insert"), uid, args.User);
            return;
        }

        _audio.PlayPvs(component.InsertSound, uid);
        UpdateCameraInterface(uid, component);
    }

    private void OnPhotoCardInteractUsing(EntityUid uid, PhotoCardComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, WriteTag))
            return;

        args.Handled = true;
        TryPromptPhotoCustomization(args.User, uid);
    }

    private bool TryTakeImage(EntityUid uid, PhotoCameraComponent component, EntityUid user, byte[] imageData, byte[]? previewData, IReadOnlyList<NetEntity> capturedEntities, float zoom)
    {
        var printCard = PrintCard(uid, component, user, imageData, previewData, capturedEntities, zoom);

        if (printCard)
        {
            _delay.TryResetDelay(uid);
            _audio.PlayPvs(component.PhotoSound, uid);
        }
        else
            _audio.PlayPvs(component.ErrorSound, uid);

        return printCard;
    }

    private bool PrintCard(EntityUid uid, PhotoCameraComponent component, EntityUid user, byte[] imageData, byte[]? previewData, IReadOnlyList<NetEntity> capturedEntities, float zoom)
    {
        if (!IsValidCardCost(uid, component))
            return false;

        if (!_material.TryChangeMaterialAmount(uid, component.CardMaterial, -component.CardCost))
        {
            _popup.PopupEntity(Loc.GetString("photo-camera-no-paper"), uid, user);

            return false;
        }

        var card = Spawn(component.CardPrototype);
        _transform.SetMapCoordinates(card, _transform.GetMapCoordinates(uid));

        if (!TryComp<PhotoCardComponent>(card, out var photo))
        {
            Log.Error($"Failed to print photo from camera {ToPrettyString(uid)} for user {ToPrettyString(user)}: spawned card {ToPrettyString(card)} from prototype '{component.CardPrototype}' is missing {nameof(PhotoCardComponent)}.");

            if (!_material.TryChangeMaterialAmount(uid, component.CardMaterial, component.CardCost))
            {
                Log.Error($"Failed to refund photo material cost for camera {ToPrettyString(uid)} after print failure: +{component.CardCost} {component.CardMaterial}.");
            }

            Del(card);
            return false;
        }

        var metadata = BuildPhotoMetadata(user, capturedEntities, component.ViewBox, zoom);
        photo.ImageData = imageData;
        photo.PreviewData = previewData;

        if (metadata is { } captureMetadata)
        {
            photo.MobsSeen = new List<EntityUid>(captureMetadata.MobsSeen);
            photo.DeadSeen = new List<EntityUid>(captureMetadata.DeadSeen);
            photo.NamesSeen = new List<string>(captureMetadata.NamesSeen);
            photo.BaseDescription = captureMetadata.Description;
            photo.CaptureData = captureMetadata.CaptureData;
        }

        photo.CreatedAt = DateTime.UtcNow;
        photo.UpdatedAt = photo.CreatedAt;

        UpdatePhotoCardAppearance(card, photo);
        UpdatePhotoCardExamineDescription(card, photo);

        _hands.TryPickupAnyHand(user, card);
        TryPromptPhotoCustomization(user, card);

        UpdateCameraInterface(uid, component);

        return true;
    }

    public void UpdatePhotoCardAppearance(EntityUid uid, PhotoCardComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, PhotoCardVisuals.PreviewImage, component.PreviewData ?? Array.Empty<byte>(), appearance);
    }

    public bool TryPreparePhotoCardData(byte[] imageData, byte[]? previewData, out byte[] preparedImageData, out byte[]? preparedPreviewData)
    {
        preparedImageData = Array.Empty<byte>();
        preparedPreviewData = null;

        if (!ValidatePngData(imageData, MAX_SIZE, MAX_WIDTH, MAX_HEIGHT, MAX_PIXELS))
            return false;

        preparedImageData = [.. imageData];

        if (previewData is { Length: > 0 } preview &&
            ValidatePngData(preview, MAX_PREVIEW_SIZE, MAX_PREVIEW_WIDTH, MAX_PREVIEW_HEIGHT, MAX_PREVIEW_PIXELS))
        {
            preparedPreviewData = [.. preview];
            return true;
        }

        preparedPreviewData = GeneratePreviewData(preparedImageData);
        return true;
    }

    public bool TrySetPhotoCardData(
        EntityUid uid,
        PhotoCardComponent component,
        byte[] imageData,
        byte[]? previewData,
        string? customName = null,
        string? customDescription = null,
        string? caption = null,
        string? baseDescription = null,
        PhotoCaptureData? captureData = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        if (!TryPreparePhotoCardData(imageData, previewData, out var preparedImageData, out var preparedPreviewData))
            return false;

        component.ImageData = preparedImageData;
        component.PreviewData = preparedPreviewData;
        component.CustomName = customName;
        component.CustomDescription = customDescription;
        component.Caption = caption;
        component.BaseDescription = baseDescription;
        component.CaptureData = captureData;
        component.NamesSeen = captureData?.RecognizedNames is { } names
            ? new List<string>(names)
            : new List<string>();
        component.MobsSeen = new List<EntityUid>();
        component.DeadSeen = new List<EntityUid>();
        component.CreatedAt = createdAt;
        component.UpdatedAt = updatedAt ?? createdAt;
        component.IsArchivedAlbumPhoto = false;

        UpdatePhotoCardAppearance(uid, component);
        UpdatePhotoCardExamineDescription(uid, component);
        UpdatePhotoCardInterface(uid, component);
        return true;
    }

    public bool TryCreatePersistentPhotoData(PhotoCardComponent component, [NotNullWhen(true)] out PersistentPhotoData? data)
    {
        data = null;
        if (component.ImageData is not { Length: > 0 } imageData)
            return false;

        data = new PersistentPhotoData
        {
            ImageData = [.. imageData],
            PreviewData = component.PreviewData is { Length: > 0 } preview ? [.. preview] : null,
            CustomName = component.CustomName,
            CustomDescription = component.CustomDescription,
            Caption = component.Caption,
            BaseDescription = component.BaseDescription,
            CaptureData = CloneCaptureData(component.CaptureData),
            CreatedAt = component.CreatedAt,
            UpdatedAt = component.UpdatedAt
        };
        return true;
    }

    public bool TryApplyPersistentPhotoData(EntityUid uid, PhotoCardComponent component, PersistentPhotoData data)
    {
        return TrySetPhotoCardData(
            uid,
            component,
            data.ImageData,
            data.PreviewData,
            data.CustomName,
            data.CustomDescription,
            data.Caption,
            data.BaseDescription,
            CloneCaptureData(data.CaptureData),
            data.CreatedAt,
            data.UpdatedAt);
    }

    private bool CanPrintPhoto(EntityUid uid, PhotoCameraComponent component)
    {
        return IsValidCardCost(uid, component) &&
               _material.CanChangeMaterialAmount(uid, component.CardMaterial, -component.CardCost);
    }

    private bool IsValidCardCost(EntityUid uid, PhotoCameraComponent component)
    {
        if (component.CardCost > 0)
            return true;

        Log.Warning($"Photo camera {ToPrettyString(uid)} has invalid {nameof(PhotoCameraComponent.CardCost)} value {component.CardCost}. Expected > 0.");
        return false;
    }

    private static bool CheckPngSignature(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) return false;
        return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
    }

    private bool ValidatePngData(byte[] data, int maxSize, int maxWidth, int maxHeight, int maxPixels)
    {
        if (data.Length == 0 || data.Length > maxSize)
            return false;

        if (!CheckPngSignature(data))
            return false;

        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var imageInfo = Image.Identify(stream);
            if (imageInfo == null)
                return false;

            if (imageInfo.Width <= 0 || imageInfo.Height <= 0)
                return false;

            if (imageInfo.Width > maxWidth || imageInfo.Height > maxHeight)
                return false;

            if ((long) imageInfo.Width * imageInfo.Height > maxPixels)
                return false;

            stream.Position = 0;
            using var image = Image.Load<Rgba32>(stream);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to parse PNG: {ex}");
            return false;
        }
    }

    private byte[]? GeneratePreviewData(byte[] imageData)
    {
        try
        {
            using var imageStream = new MemoryStream(imageData, writable: false);
            using var image = Image.Load<Rgba32>(imageStream);
            if (!image.DangerousTryGetSinglePixelMemory(out var imageMemory))
                return null;

            var stageOne = DownscaleBox(imageMemory.Span, image.Width, image.Height, PreviewSampleSize, PreviewSampleSize);
            var stageTwo = DownscaleBox(stageOne, PreviewSampleSize, PreviewSampleSize, PreviewSize, PreviewSize);

            using var miniature = new Image<Rgba32>(PreviewSize, PreviewSize);
            if (!miniature.DangerousTryGetSinglePixelMemory(out var miniatureMemory))
                return null;

            stageTwo.CopyTo(miniatureMemory.Span);

            using var previewStream = new MemoryStream();
            miniature.SaveAsPng(previewStream);

            var previewData = previewStream.ToArray();
            return ValidatePngData(previewData, MAX_PREVIEW_SIZE, MAX_PREVIEW_WIDTH, MAX_PREVIEW_HEIGHT, MAX_PREVIEW_PIXELS)
                ? previewData
                : null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to generate photo preview PNG: {ex}");
            return null;
        }
    }

    private static Rgba32[] DownscaleBox(ReadOnlySpan<Rgba32> source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var result = new Rgba32[targetWidth * targetHeight];

        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY0 = y * sourceHeight / targetHeight;
            var sourceY1 = (y + 1) * sourceHeight / targetHeight;
            if (sourceY1 <= sourceY0)
                sourceY1 = sourceY0 + 1;

            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX0 = x * sourceWidth / targetWidth;
                var sourceX1 = (x + 1) * sourceWidth / targetWidth;
                if (sourceX1 <= sourceX0)
                    sourceX1 = sourceX0 + 1;

                var sumR = 0;
                var sumG = 0;
                var sumB = 0;
                var sumA = 0;
                var count = 0;

                for (var sourceY = sourceY0; sourceY < sourceY1; sourceY++)
                {
                    var rowOffset = sourceY * sourceWidth;
                    for (var sourceX = sourceX0; sourceX < sourceX1; sourceX++)
                    {
                        var pixel = source[rowOffset + sourceX];
                        sumR += pixel.R;
                        sumG += pixel.G;
                        sumB += pixel.B;
                        sumA += pixel.A;
                        count++;
                    }
                }

                if (count <= 0)
                {
                    result[y * targetWidth + x] = default;
                    continue;
                }

                result[y * targetWidth + x] = new Rgba32(
                    (byte) (sumR / count),
                    (byte) (sumG / count),
                    (byte) (sumB / count),
                    (byte) (sumA / count));
            }
        }

        return result;
    }

    private PhotoCaptureMetadata BuildPhotoMetadata(EntityUid photographer, IReadOnlyList<NetEntity> capturedEntities, Vector2 viewBox, float zoom)
    {
        var areaWidth = Math.Max(1, (int) MathF.Ceiling(viewBox.X * Math.Clamp(zoom, 0.01f, 10f)));
        var areaHeight = Math.Max(1, (int) MathF.Ceiling(viewBox.Y * Math.Clamp(zoom, 0.01f, 10f)));

        var descriptionLines = new List<string>
        {
            Loc.GetString("photo-card-area-description", ("width", areaWidth), ("height", areaHeight))
        };

        var mobsSeen = new List<EntityUid>();
        var deadSeen = new List<EntityUid>();
        var namesSeen = new List<string>();
        var subjects = new List<PhotoCapturedSubjectData>();
        var seen = new HashSet<EntityUid>();

        foreach (var netEntity in capturedEntities)
        {
            var entity = GetEntity(netEntity);
            if (!Exists(entity) || !seen.Add(entity))
                continue;

            if (!TryComp<MobStateComponent>(entity, out var mobState))
                continue;

            var entityName = Identity.Name(entity, EntityManager, photographer);
            mobsSeen.Add(entity);

            if (mobState.CurrentState == MobState.Dead)
                deadSeen.Add(entity);

            if (TryComp<HumanoidAppearanceComponent>(entity, out _))
                namesSeen.Add(entityName);

            var heldItems = GetHeldItems(entity, photographer);
            var gender = GetPhotoGender(entity);
            subjects.Add(new PhotoCapturedSubjectData(
                entityName,
                mobState.CurrentState,
                gender,
                new List<string>(heldItems),
                HasComp<HumanoidAppearanceComponent>(entity)));
            descriptionLines.Add(BuildEntityDescriptionLine(entityName, mobState.CurrentState, heldItems, gender));
        }

        var captureData = new PhotoCaptureData(
            areaWidth,
            areaHeight,
            subjects,
            new List<string>(namesSeen));

        return new PhotoCaptureMetadata(
            mobsSeen,
            deadSeen,
            namesSeen,
            string.Join("\n", descriptionLines),
            captureData);
    }

    private static float SanitizeCaptureZoom(PhotoCameraComponent component, float zoom)
    {
        var minZoom = Math.Min(component.MinZoom, component.MaxZoom);
        var maxZoom = Math.Max(component.MinZoom, component.MaxZoom);

        if (!float.IsFinite(zoom))
            return maxZoom;

        return Math.Clamp(zoom, minZoom, maxZoom);
    }

    private List<string> GetHeldItems(EntityUid entity, EntityUid photographer)
    {
        var heldItems = new List<string>();
        if (!TryComp<HandsComponent>(entity, out var hands))
            return heldItems;

        foreach (var held in _hands.EnumerateHeld((entity, hands)))
        {
            if (!Exists(held))
                continue;

            heldItems.Add(Identity.Name(held, EntityManager, photographer));
        }

        return heldItems;
    }

    private string BuildEntityDescriptionLine(string entityName, MobState state, IReadOnlyList<string> heldItems, string gender)
    {
        var hasState = TryGetStateDescription(state, out var stateDescription);
        var hasHeldItems = heldItems.Count > 0;
        var heldItemsText = JoinCommaList(heldItems);

        return (hasState, hasHeldItems) switch
        {
            (true, true) => Loc.GetString("photo-card-mob-description-state-holding",
                ("name", entityName),
                ("state", stateDescription),
                ("gender", gender),
                ("items", heldItemsText)),
            (true, false) => Loc.GetString("photo-card-mob-description-state",
                ("name", entityName),
                ("state", stateDescription)),
            (false, true) => Loc.GetString("photo-card-mob-description-holding",
                ("name", entityName),
                ("gender", gender),
                ("items", heldItemsText)),
            _ => Loc.GetString("photo-card-mob-description",
                ("name", entityName))
        };
    }

    private string GetPhotoGender(EntityUid entity)
    {
        if (TryComp<HumanoidAppearanceComponent>(entity, out var humanoid))
            return GetGenderKey(humanoid.Gender);

        if (TryComp<GrammarComponent>(entity, out var grammar) && grammar.Gender is { } grammarGender)
            return GetGenderKey(grammarGender);

        return "other";
    }

    private static string GetGenderKey(Gender gender)
    {
        return gender switch
        {
            Gender.Male => "male",
            Gender.Female => "female",
            Gender.Epicene => "epicene",
            Gender.Neuter => "neuter",
            _ => "other"
        };
    }

    private static string JoinCommaList(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return string.Empty;

        return values.Count == 1 ? values[0] : string.Join(", ", values);
    }

    private bool TryGetStateDescription(MobState state, out string description)
    {
        switch (state)
        {
            case MobState.Critical:
                description = Loc.GetString("photo-card-mob-state-critical");
                return true;
            case MobState.Dead:
                description = Loc.GetString("photo-card-mob-state-dead");
                return true;
        }

        description = string.Empty;
        return false;
    }

    private void TryPromptPhotoCustomization(EntityUid user, EntityUid photo)
    {
        if (!Exists(user) || !Exists(photo))
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        if (!TryComp<PhotoCardComponent>(photo, out var photoCard))
            return;

        PromptPhotoName(
            actor.PlayerSession,
            user,
            photo,
            photoCard.CustomName,
            photoCard.CustomDescription);
    }

    private void PromptPhotoName(
        ICommonSession session,
        EntityUid user,
        EntityUid photo,
        string? customName,
        string? customDescription)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        _quickDialog.OpenDialogPrefilled<string>(
            session,
            Loc.GetString("photo-camera-customize-name-title"),
            string.Empty,
            okAction: value =>
            {
                var name = TrimToLength(value, MaxCustomNameLength);
                SetPhotoCustomName(user, photo, name);
                PromptPhotoDescription(session, user, photo, name, customDescription);
            },
            initialValue: customName);
    }

    private void PromptPhotoDescription(
        ICommonSession session,
        EntityUid user,
        EntityUid photo,
        string? customName,
        string? customDescription)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        _quickDialog.OpenDialogPrefilled<LongString>(
            session,
            Loc.GetString("photo-camera-customize-description-title"),
            string.Empty,
            okAction: value =>
            {
                var description = TrimToLength(value, MaxCustomDescriptionLength);
                SetPhotoCustomDescription(user, photo, description);
                PromptPhotoCaption(session, user, photo, customName, description);
            },
            initialValue: customDescription);
    }

    private void PromptPhotoCaption(
        ICommonSession session,
        EntityUid user,
        EntityUid photo,
        string? customName,
        string? customDescription)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        if (!TryComp<PhotoCardComponent>(photo, out var photoCard))
            return;

        _quickDialog.OpenDialogPrefilled<LongString>(
            session,
            Loc.GetString("photo-camera-customize-caption-title"),
            string.Empty,
            okAction: value =>
            {
                var caption = TrimToLength(value, MaxCustomCaptionLength);
                SetPhotoCaption(user, photo, caption);
            },
            initialValue: photoCard.Caption);
    }

    private void SetPhotoCustomName(
        EntityUid user,
        EntityUid photo,
        string? customName)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        if (!TryComp<PhotoCardComponent>(photo, out var photoCard))
            return;

        if (photoCard.CustomName == customName)
            return;

        photoCard.CustomName = customName;
        TouchPhotoCard(photoCard);
        UpdatePhotoCardInterface(photo, photoCard);
    }

    private void SetPhotoCustomDescription(
        EntityUid user,
        EntityUid photo,
        string? customDescription)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        if (!TryComp<PhotoCardComponent>(photo, out var photoCard))
            return;

        if (photoCard.CustomDescription == customDescription)
            return;

        photoCard.CustomDescription = customDescription;
        TouchPhotoCard(photoCard);
        UpdatePhotoCardExamineDescription(photo, photoCard);
    }

    private void SetPhotoCaption(
        EntityUid user,
        EntityUid photo,
        string? caption)
    {
        if (!CanContinueCustomization(user, photo))
            return;

        if (!TryComp<PhotoCardComponent>(photo, out var photoCard))
            return;

        if (photoCard.Caption == caption)
            return;

        photoCard.Caption = caption;
        TouchPhotoCard(photoCard);
        UpdatePhotoCardInterface(photo, photoCard);
    }

    private bool CanContinueCustomization(EntityUid user, EntityUid photo)
    {
        if (!Exists(user) || !Exists(photo))
            return false;

        if (_hands.IsHolding(user, photo))
            return true;

        return _interactionSystem.InRangeUnobstructed(user, photo);
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            trimmed = trimmed[..maxLength];

        return trimmed;
    }

    public void UpdatePhotoCardExamineDescription(EntityUid uid, PhotoCardComponent component)
    {
        var baseDescription = component.BaseDescription;
        var customDescription = component.CustomDescription;

        var descriptionParts = new List<string>();

        if (component.IsArchivedAlbumPhoto)
            descriptionParts.Add(Loc.GetString("photo-card-archived-description"));

        if (!string.IsNullOrWhiteSpace(customDescription))
        {
            descriptionParts.Add(string.IsNullOrWhiteSpace(baseDescription)
                ? customDescription
                : $"{customDescription} - {baseDescription}");
        }
        else if (!string.IsNullOrWhiteSpace(baseDescription))
        {
            descriptionParts.Add(baseDescription);
        }

        var composedDescription = string.Join(" ", descriptionParts);

        if (!string.IsNullOrWhiteSpace(composedDescription))
            _metaData.SetEntityDescription(uid, composedDescription);
        else
            _metaData.SetEntityDescription(uid, string.Empty);
    }

    // Photo Card

    private void OnOpenCardInterface(EntityUid uid, PhotoCardComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdatePhotoCardInterface(uid, component);
    }

    private void UpdatePhotoCardInterface(EntityUid uid, PhotoCardComponent component)
    {
        var state = new PhotoCardUiState(component.ImageData, component.CustomName, component.Caption);
        _userInterface.SetUiState(uid, PhotoCardUiKey.Key, state);
    }

    private static PhotoCaptureData? CloneCaptureData(PhotoCaptureData? data)
    {
        if (data == null)
            return null;

        var subjects = new List<PhotoCapturedSubjectData>(data.Subjects.Count);
        foreach (var subject in data.Subjects)
        {
            subjects.Add(new PhotoCapturedSubjectData(
                subject.DisplayName,
                subject.State,
                subject.GenderKey,
                new List<string>(subject.HeldItems),
                subject.IsHumanoid));
        }

        return new PhotoCaptureData(
            data.AreaWidth,
            data.AreaHeight,
            subjects,
            new List<string>(data.RecognizedNames));
    }

    private static void TouchPhotoCard(PhotoCardComponent component)
    {
        component.UpdatedAt = DateTime.UtcNow;
        component.CreatedAt ??= component.UpdatedAt;
    }

    private sealed record PhotoCaptureMetadata(
        List<EntityUid> MobsSeen,
        List<EntityUid> DeadSeen,
        List<string> NamesSeen,
        string Description,
        PhotoCaptureData CaptureData);
}


