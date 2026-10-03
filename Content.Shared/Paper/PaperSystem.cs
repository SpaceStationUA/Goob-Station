// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.UserInterface;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Containers; // Pirate: persistent diary pages
using Robust.Shared.Audio.Systems;
using static Content.Shared.Paper.PaperComponent;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
// Starlight-start
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
// Starlight-end

#region Pirate: paperwork tags
using Robust.Shared.Network;
using Content.Shared.Mind;
using System.Globalization;
using System.Text.RegularExpressions;
using Content.Shared._Pirate.Paper;
using Content.Shared._Pirate.PersistentText;
using Content.Shared.Access.Systems;
using Content.Shared.Nutrition; // Pirate: persistent diary pages (not edible)
using Content.Shared.Hands.EntitySystems; // Pirate: persistent diary pages
using Content.Shared.GameTicking;
using Content.Shared.Station;
#endregion


namespace Content.Shared.Paper;

public sealed class PaperSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IdentitySystem _identitySystem = default!; // Starlight-edit
    [Dependency] private readonly SharedMindSystem _mind = default!; // Pirate: persistent text (diaries)
    [Dependency] private readonly SharedHandsSystem _hands = default!; // Pirate: persistent diary pages
    [Dependency] private readonly SharedContainerSystem _containers = default!; // Pirate: persistent diary pages

    private static readonly ProtoId<TagPrototype> WriteIgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    // Pirate: persistent diary pages
    private static readonly ProtoId<TagPrototype> PaperTag = "Paper";
    private static readonly ProtoId<TagPrototype> BookTag = "Book";
    private const string SheetPrototype = "Paper";
    private const string SignatureStampState = "paper_stamp-signature";
    #region Pirate: paperwork tags
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    private const int StationBaseYear = Content.Shared._Pirate.PirateStationCalendar.CurrentYear; // Pirate: camera (nanochat gallery)
    private static readonly Regex StationCodeRegex = new(@"\b[A-Z]{2,5}-\d{1,4}\b", RegexOptions.Compiled);
    private static readonly Regex StationLabelRegex = new(
        @"(?:^|\s)(?:Station|Станція|Станция)\s*:\s*(?<value>[^\s,.;:]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    #endregion

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PaperComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PaperComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<PaperComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PaperComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaperComponent, PaperInputTextMessage>(OnInputTextMessage);
        SubscribeLocalEvent<PaperComponent, PaperMacroMenuUsedMessage>(OnMacroMenuUsedMessage); // Pirate: paperwork tags
        SubscribeLocalEvent<PaperComponent, PaperSignatureRequestMessage>(OnSignatureRequest); // Starlight-edit
        SubscribeLocalEvent<PaperComponent, PaperPageActionMessage>(OnPageActionMessage); // Pirate: persistent diary pages
        SubscribeLocalEvent<PaperPagesComponent, IngestibleEvent>(OnPagesIngestible); // Pirate: persistent diary pages


        SubscribeLocalEvent<RandomPaperContentComponent, MapInitEvent>(OnRandomPaperContentMapInit);

        SubscribeLocalEvent<ActivateOnPaperOpenedComponent, PaperWriteEvent>(OnPaperWrite);
        SubscribeLocalEvent<PaperComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnMapInit(Entity<PaperComponent> entity, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(entity.Comp.Content))
        {
            SetContent(entity, Loc.GetString(entity.Comp.Content));
        }
    }

    private void OnRandomPaperContentMapInit(Entity<RandomPaperContentComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(ent, out var paperComp))
        {
            Log.Warning($"{ToPrettyString(ent)} has a {nameof(RandomPaperContentComponent)} but no {nameof(PaperComponent)}!");
            RemCompDeferred(ent, ent.Comp);
            return;
        }
        var dataset = _protoMan.Index(ent.Comp.Dataset);
        // Intentionally not using the Pick overload that directly takes a LocalizedDataset,
        // because we want to get multiple attributes from the same pick.
        var pick = _random.Pick(dataset.Values);

        // Name
        _metaSystem.SetEntityName(ent, Loc.GetString(pick));
        // Description
        _metaSystem.SetEntityDescription(ent, Loc.GetString($"{pick}.desc"));
        // Content
        SetContent((ent, paperComp), Loc.GetString($"{pick}.content"));

        // Our work here is done
        RemCompDeferred(ent, ent.Comp);
    }

    private void OnInit(Entity<PaperComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Mode = PaperAction.Read;
        UpdateUserInterface(entity);

        if (TryComp<AppearanceComponent>(entity, out var appearance))
        {
            if (entity.Comp.Content != "")
                _appearance.SetData(entity, PaperVisuals.Status, PaperStatus.Written, appearance);

            if (entity.Comp.StampState != null)
                _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
        }
    }

    private void BeforeUIOpen(Entity<PaperComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        entity.Comp.Mode = PaperAction.Read;
        if (entity.Comp.WriteSessionIgnoresStampsActor == args.User)
            entity.Comp.WriteSessionIgnoresStampsActor = null;
        UpdateUserInterface(entity);
    }

    private void OnExamined(Entity<PaperComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(PaperComponent)))
        {
            if (entity.Comp.Content != "")
            {
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-has-words",
                        ("paper", entity)
                    )
                );
            }

            if (entity.Comp.StampedBy.Count > 0)
            {
                var commaSeparated =
                    string.Join(", ", entity.Comp.StampedBy.Select(s => Loc.GetString(s.StampedName)));
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-stamped-by",
                        ("paper", entity),
                        ("stamps", commaSeparated))
                );
            }
        }
    }

    private void OnInteractUsing(Entity<PaperComponent> entity, ref InteractUsingEvent args)
    {
        // Pirate: persistent diary pages - a loose sheet tucked into the book becomes a leaf,
        // carrying its text and its stamps over as that leaf's signatures.
        if (TryComp<PaperPagesComponent>(entity, out var insertedPages) && IsLooseSheet(args.Used))
        {
            TryInsertSheet(entity, insertedPages, args.Used, args.User);
            args.Handled = true;
            return;
        }

        // only allow editing if there are no stamps or when using a cyberpen
        var editable = !IsWriteLocked(entity) || _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag);
        if (_tagSystem.HasTag(args.Used, WriteTag))
        {
            // Pirate: stamped/signed documents are final - remember whether this write
            // session was started by a pen that may ignore stamps (cyberpen), so save
            // attempts arriving later (page flips, direct messages) are checked against it.
            if (editable)
            {
                if (entity.Comp.EditingDisabled)
                {
                    var paperEditingDisabledMessage = Loc.GetString("paper-tamper-proof-modified-message");
                    _popupSystem.PopupClient(paperEditingDisabledMessage, entity, args.User);

                    args.Handled = true;
                    return;
                }

                var ev = new PaperWriteAttemptEvent(entity.Owner);
                RaiseLocalEvent(args.User, ref ev);
                if (ev.Cancelled)
                {
                    if (ev.FailReason is not null)
                    {
                        var fileWriteMessage = Loc.GetString(ev.FailReason);
                        _popupSystem.PopupClient(fileWriteMessage, entity.Owner, args.User);
                    }

                    args.Handled = true;
                    return;
                }

                var writeEvent = new PaperWriteEvent(args.User, entity);
                RaiseLocalEvent(args.Used, ref writeEvent);

                entity.Comp.WriteSessionIgnoresStampsActor =
                    _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag) ? args.User : null;

                entity.Comp.Mode = PaperAction.Write;
                _uiSystem.OpenUi(entity.Owner, PaperUiKey.Key, args.User);
                UpdateUserInterface(entity);
            }

            args.Handled = true;
            return;
        }

        // If a stamp, attempt to stamp paper
        if (TryComp<StampComponent>(args.Used, out var stampComp) && TryStamp(entity, GetStampInfo(stampComp), stampComp.StampState))
        {
            // successfully stamped, play popup
            var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other",
                    ("user", args.User),
                    ("target", args.Target),
                    ("stamp", args.Used));

            _popupSystem.PopupEntity(stampPaperOtherMessage, args.User, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
            var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self",
                    ("target", args.Target),
                    ("stamp", args.Used));
            _popupSystem.PopupClient(stampPaperSelfMessage, args.User, args.User);

            _audio.PlayPredicted(stampComp.Sound, entity, args.User);

            UpdateUserInterface(entity);
        }
    }

    /// <summary>
    /// Pirate: a document is final once it carries stamps, and a leaf of a paginated item
    /// once that leaf carries signatures - a stamped sheet tucked into a diary locks its
    /// leaf exactly like a stamp locks a loose sheet. <paramref name="page"/> picks the
    /// leaf to inspect, defaulting to the one currently on screen.
    /// </summary>
    private bool IsWriteLocked(Entity<PaperComponent> entity, int? page = null)
    {
        if (entity.Comp.StampedBy.Count > 0)
            return true;

        if (TryComp<PaperPagesComponent>(entity, out var pages))
        {
            // CurrentPageIndex also keeps the leaf list non-empty, so the clamp is safe.
            var index = CurrentPageIndex(pages);
            if (page != null)
                index = Math.Clamp(page.Value, 0, pages.Pages.Count - 1);

            return pages.Pages[index].Signatures.Count > 0;
        }

        return false;
    }

    private static StampDisplayInfo GetStampInfo(StampComponent stamp)
    {
        return new StampDisplayInfo
        {
            StampedName = stamp.StampedName,
            StampedColor = stamp.StampedColor, // Goob stamp
            StampLargeIcon = stamp.StampLargeIcon // goob Stamp
        };
    }

    private void OnInputTextMessage(Entity<PaperComponent> entity, ref PaperInputTextMessage args)
    {
        if (!TryWriteText(entity, args.Actor, args.Text, args.Page))
            return;

        entity.Comp.Mode = PaperAction.Read;
        if (entity.Comp.WriteSessionIgnoresStampsActor == args.Actor)
            entity.Comp.WriteSessionIgnoresStampsActor = null;
        UpdateUserInterface(entity);
    }

    /// <summary>
    /// Checks the write permission and applies the text to the item - to the whole document,
    /// or, for paginated items, only to the leaf the request names (falling back to the one
    /// on screen when it names none).
    /// </summary>
    private bool TryWriteText(Entity<PaperComponent> entity, EntityUid actor, string rawText, int? page = null)
    {
        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(actor, ref ev);
        if (ev.Cancelled)
            return false;

        // Pirate: persistent text (diaries) - only the bound owner may write;
        // unbound items bind to the first writer (e.g. spawned outside a loadout).
        if (TryComp<PersistentTextComponent>(entity, out var persistentText))
        {
            if (!CanWrite(entity.Owner, persistentText, actor))
            {
                _popupSystem.PopupClient(Loc.GetString("persistent-text-cant-write"), entity.Owner, actor);
                return false;
            }

            BindPersistentTextOwner(entity.Owner, persistentText, actor);
        }

        // Pirate: persistent diary pages - the request names the leaf its text was typed
        // on. pages.CurrentPage is shared by every viewer, so a flip by another player
        // must never reroute the write; an out-of-range leaf means the leaves changed
        // under the sender, so the write is rejected instead of saved somewhere else.
        int? targetPage = null;
        if (TryComp<PaperPagesComponent>(entity, out var pages))
        {
            if (page == null)
            {
                targetPage = CurrentPageIndex(pages);
            }
            else
            {
                var requested = page.Value;
                if (requested < 0 || requested >= pages.Pages.Count)
                    return false;

                targetPage = requested;
            }
        }

        // Pirate: stamped/signed documents and leaves are final - only a write session
        // opened with a stamp-ignoring pen (cyberpen) may still write into them. This also
        // covers a flip carrying an edit onto a leaf that got signed while the editor was open.
        if (IsWriteLocked(entity, targetPage) && entity.Comp.WriteSessionIgnoresStampsActor != actor)
            return false;

        // Pirate: player text must never contain the reserved PaperPageFormat/cover-meta
        // control characters - otherwise a typed marker forges leaves and signatures later.
        var processedText = PaperPageFormat.Sanitize(ExpandPaperMacros(entity, actor, rawText)); // Pirate: paperwork tags

        if (processedText.Length > entity.Comp.ContentSize) // Pirate: paperwork tags
            return false;

        // Pirate: persistent diary pages - a paginated item only rewrites the leaf the
        // request names, and only after that index has been validated.
        if (pages != null)
        {
            if (!SetPageText(entity, pages, targetPage!.Value, processedText))
                return false;
        }
        else
        {
            SetContent(entity, processedText); // Pirate: paperwork tags
        }

        var paperStatus = string.IsNullOrWhiteSpace(processedText) ? PaperStatus.Blank : PaperStatus.Written; // Pirate: paperwork tags

        if (TryComp<AppearanceComponent>(entity, out var appearance))
            _appearance.SetData(entity, PaperVisuals.Status, paperStatus, appearance);

        if (TryComp(entity, out MetaDataComponent? meta))
            _metaSystem.SetEntityDescription(entity, "", meta);

        _adminLogger.Add(LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(actor):player} has written on {ToPrettyString(entity):entity} the following text: {processedText}"); // Pirate: paperwork tags

        _audio.PlayPvs(entity.Comp.Sound, entity);

        return true;
    }

    #region Pirate: persistent text (diaries)

    /// <summary>
    /// Checks whether the actor may write into the persistent text entity.
    /// Unbound entities can be written by anyone, but bind to the first writer;
    /// afterwards only the bound character may write.
    /// </summary>
    public bool CanWrite(EntityUid uid, PersistentTextComponent component, EntityUid actor)
    {
        if (component.OwnerCharacterName == null)
            return true;

        if (!string.Equals(component.OwnerKind, PersistentTextOwnerKinds.Profile, StringComparison.Ordinal))
            return true;

        // Pirate: persistent text (diaries) - the check is by CHARACTER, not by the mind.
        // Exiting and re-entering a character wipes and recreates the mind, which used to
        // lock the diary even for its rightful owner. The character is identified by its
        // in-world name plus the account behind it (session first, mind only as a fallback
        // so client-side prediction evaluates the same way).
        if (!string.Equals(Name(actor), component.OwnerCharacterName, StringComparison.Ordinal))
            return false;

        if (component.OwnerUserId == null)
            return true;

        NetUserId? userId = null;
        if (TryComp<ActorComponent>(actor, out var actorComp))
            userId = actorComp.PlayerSession.UserId;
        else if (_mind.TryGetMind(actor, out _, out var mind))
            userId = mind.UserId;

        // Bound by character name and userId: another player's character
        // with the same name can never take over the diary.
        return userId != null && component.OwnerUserId == userId.Value;
    }

    /// <summary>
    /// Binds the persistent text entity to the first writer's character and renames it after them.
    /// </summary>
    private void BindPersistentTextOwner(EntityUid uid, PersistentTextComponent component, EntityUid actor)
    {
        if (!component.SupportCharacterName || component.OwnerCharacterName != null)
            return;

        if (!string.Equals(component.OwnerKind, PersistentTextOwnerKinds.Profile, StringComparison.Ordinal))
            return;

        // Pirate: bind to the CHARACTER (in-world name + account), not the mind —
        // the mind is replaced whenever a player exits and re-enters their character.
        var characterName = Name(actor);
        if (string.IsNullOrWhiteSpace(characterName))
            return;

        NetUserId? userId = null;
        if (TryComp<ActorComponent>(actor, out var actorComp))
            userId = actorComp.PlayerSession.UserId;
        else if (_mind.TryGetMind(actor, out _, out var mind))
            userId = mind.UserId;

        if (userId == null)
            return;

        component.OwnerCharacterName = characterName;
        component.OwnerUserId = userId.Value;
        UpdatePersistentTextName(uid, component);
    }

    /// <summary>
    /// Appends the bound character name to the entity name once the diary is bound
    /// ("<base name> <character>"), keeping any loadout-customized base name.
    /// Only renames when AppendOwnerName is set (diaries rename, regular books keep their name).
    /// </summary>
    public void UpdatePersistentTextName(EntityUid uid, PersistentTextComponent? component = null, MetaDataComponent? meta = null)
    {
        if (!Resolve(uid, ref component, ref meta, false))
            return;

        if (string.IsNullOrWhiteSpace(component.OwnerCharacterName) ||
            !component.AppendOwnerName)
            return;

        var suffix = " " + component.OwnerCharacterName;

        // Remember the base name (prototype or loadout-customized) so suffixes never stack.
        var baseName = component.BaseEntityName;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            var current = meta.EntityName;
            baseName = current.EndsWith(suffix, StringComparison.Ordinal)
                ? current[..^suffix.Length].TrimEnd()
                : current;
        }

        component.BaseEntityName = baseName;
        _metaSystem.SetEntityName(uid, baseName + suffix, meta);
    }

    #endregion

    #region Pirate: persistent diary pages

    /// <summary>
    /// Pirate: a diary you page through is not food - no eat verb, no force-feeding,
    /// whatever the eater's stomach claims to digest.
    /// </summary>
    private void OnPagesIngestible(Entity<PaperPagesComponent> entity, ref IngestibleEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPageActionMessage(Entity<PaperComponent> entity, ref PaperPageActionMessage args)
    {
        if (!TryComp<PaperPagesComponent>(entity, out var pages))
            return;

        switch (args.Action)
        {
            case PaperPageAction.Turn:
                // The flip carries the text still being typed, so the leaf being left
                // behind keeps it instead of the window silently dropping the edit.
                if (args.Text != null && !TryWriteText(entity, args.Actor, args.Text, args.TextPage))
                    return;

                SetCurrentPage(entity, pages, args.Page);
                return;

            case PaperPageAction.Add:
                if (!CanEditPages(entity, args.Actor))
                    return;

                // Pirate: persistent diary pages - a fresh leaf is made of paper you carry.
                var material = FindSheetToUse(args.Actor);
                if (material == null)
                {
                    _popupSystem.PopupClient(
                        Loc.GetString("paper-page-add-need-paper"),
                        entity.Owner,
                        args.Actor,
                        PopupType.SmallCaution);
                    return;
                }

                // A failed write stops the flow: the sheet is not consumed and no page
                // is added for text that never got saved.
                if (args.Text != null && !TryWriteText(entity, args.Actor, args.Text, args.TextPage))
                    return;

                QueueDel(material);
                AddPage(entity, pages);
                _audio.PlayPvs(entity.Comp.Sound, entity);

                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"{ToPrettyString(args.Actor):player} added a page to {ToPrettyString(entity):entity}");
                return;

            case PaperPageAction.Remove:
                if (!CanEditPages(entity, args.Actor))
                    return;

                // Pirate: persistent diary pages - tearing a leaf out hands you the sheet
                // itself, with the very same text and signatures, instead of destroying it.
                var torn = RemovePage(entity, pages);
                if (torn == null)
                    return;

                TornOutSheet(args.Actor, torn);
                _audio.PlayPvs(entity.Comp.Sound, entity);
                _popupSystem.PopupClient(
                    Loc.GetString("paper-page-torn-out", ("target", entity.Owner)),
                    args.Actor,
                    args.Actor);

                _adminLogger.Add(LogType.Chat, LogImpact.Medium,
                    $"{ToPrettyString(args.Actor):player} tore a page out of {ToPrettyString(entity):entity}");
                return;
        }
    }

    /// <summary>
    /// Pirate: persistent text (diaries) - adding or tearing out leaves is writing,
    /// so it obeys the same owner-only rule.
    /// </summary>
    private bool CanEditPages(Entity<PaperComponent> entity, EntityUid actor)
    {
        if (!TryComp<PersistentTextComponent>(entity, out var persistentText))
            return true;

        if (CanWrite(entity.Owner, persistentText, actor))
            return true;

        _popupSystem.PopupClient(Loc.GetString("persistent-text-cant-write"), entity.Owner, actor);
        return false;
    }

    private void SetCurrentPage(Entity<PaperComponent> entity, PaperPagesComponent pages, int page)
    {
        if (pages.Pages.Count == 0)
            pages.Pages.Add(new PaperPage());

        pages.CurrentPage = Math.Clamp(page, 0, pages.Pages.Count - 1);
        UpdateUserInterface(entity);
    }

    private void AddPage(Entity<PaperComponent> entity, PaperPagesComponent pages)
    {
        var index = CurrentPageIndex(pages);
        pages.Pages.Insert(index + 1, new PaperPage());
        pages.CurrentPage = index + 1;
        SyncPages(entity, pages);
    }

    /// <summary>
    /// Pirate: tears the leaf on screen out of the book and returns it, or null when it is
    /// the last leaf - a book always keeps at least one.
    /// </summary>
    private PaperPage? RemovePage(Entity<PaperComponent> entity, PaperPagesComponent pages)
    {
        if (pages.Pages.Count <= 1)
            return null;

        var index = CurrentPageIndex(pages);
        var torn = pages.Pages[index];
        pages.Pages.RemoveAt(index);
        pages.CurrentPage = Math.Min(index, pages.Pages.Count - 1);
        SyncPages(entity, pages);
        return torn;
    }

    /// <summary>
    /// Pirate: a sheet that can be used as book material - paper, but not a book,
    /// so nobody's reading material gets eaten to make a leaf.
    /// </summary>
    private bool IsLooseSheet(EntityUid uid)
    {
        if (!HasComp<PaperComponent>(uid))
            return false;

        return _tagSystem.HasTag(uid, PaperTag) && !_tagSystem.HasTag(uid, BookTag);
    }

    /// <summary>Looks for sheet material in the actor's hands and inventory.</summary>
    private EntityUid? FindSheetToUse(EntityUid actor)
    {
        foreach (var container in _containers.GetAllContainers(actor))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (IsLooseSheet(contained))
                    return contained;
            }
        }

        return null;
    }

    /// <summary>
    /// Pirate: hands the torn-out leaf to the actor as an actual sheet of paper - same text,
    /// same signatures, and a signature stamp on the sprite to show it was signed.
    /// </summary>
    private void TornOutSheet(EntityUid actor, PaperPage leaf)
    {
        var sheet = Spawn(SheetPrototype, Transform(actor).Coordinates);
        if (!TryComp<PaperComponent>(sheet, out var paper))
            return;

        if (leaf.Signatures.Count > 0)
        {
            paper.StampedBy = new List<StampDisplayInfo>(leaf.Signatures);
            paper.StampState = SignatureStampState;

            if (TryComp<AppearanceComponent>(sheet, out var appearance))
                _appearance.SetData(sheet, PaperVisuals.Stamp, paper.StampState, appearance);
        }

        SetContent((sheet, paper), PaperPageFormat.Sanitize(leaf.Content));

        // Hands full? The sheet just stays where it was dropped.
        _hands.TryPickupAnyHand(actor, sheet);
    }

    /// <summary>
    /// Pirate: tucks a loose sheet into the book as a new leaf, carrying its text and its
    /// stamps over as that leaf's signatures, and consumes the sheet.
    /// </summary>
    private bool TryInsertSheet(Entity<PaperComponent> entity, PaperPagesComponent pages, EntityUid sheet, EntityUid actor)
    {
        if (!CanEditPages(entity, actor))
            return false;

        if (!TryComp<PaperComponent>(sheet, out var sheetPaper))
            return false;

        var index = CurrentPageIndex(pages);
        pages.Pages.Insert(index + 1, new PaperPage
        {
            Content = PaperPageFormat.Sanitize(sheetPaper.Content),
            Signatures = new List<StampDisplayInfo>(sheetPaper.StampedBy),
        });
        pages.CurrentPage = index + 1;
        SyncPages(entity, pages);

        var sheetName = ToPrettyString(sheet);
        QueueDel(sheet);

        _popupSystem.PopupClient(
            Loc.GetString("paper-page-inserted", ("target", entity.Owner)),
            actor,
            actor);
        _audio.PlayPvs(entity.Comp.Sound, entity);

        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(actor):player} tucked {sheetName} into {ToPrettyString(entity):entity}");
        return true;
    }

    /// <summary>
    /// Pirate: writes into one concrete leaf, validating the index first - pages.CurrentPage
    /// is shared by every viewer and may have been flipped since the text was typed.
    /// </summary>
    private bool SetPageText(Entity<PaperComponent> entity, PaperPagesComponent pages, int index, string text)
    {
        if (index < 0 || index >= pages.Pages.Count)
            return false;

        pages.Pages[index].Content = text;
        SyncPages(entity, pages);
        return true;
    }

    /// <summary>
    /// Pirate: persistent diary pages - marks the leaf on screen with a signature.
    /// Returns false when that name already signed this leaf.
    /// </summary>
    public bool SignPage(Entity<PaperComponent> entity, PaperPagesComponent pages, string signatureName)
    {
        if (string.IsNullOrWhiteSpace(signatureName))
            return false;

        var page = pages.Pages[CurrentPageIndex(pages)];
        if (page.Signatures.Any(stamp => string.Equals(stamp.StampedName, signatureName, StringComparison.Ordinal)))
            return false;

        page.Signatures.Add(new StampDisplayInfo
        {
            StampedName = signatureName,
            StampedColor = PaperPageFormat.SignatureColor,
        });

        SyncPages(entity, pages);
        return true;
    }

    /// <summary>
    /// Index of the leaf on screen: clamped into range and never failing on an empty book.
    /// </summary>
    private static int CurrentPageIndex(PaperPagesComponent pages)
    {
        if (pages.Pages.Count == 0)
            pages.Pages.Add(new PaperPage());

        return Math.Clamp(pages.CurrentPage, 0, pages.Pages.Count - 1);
    }

    /// <summary>
    /// Pirate: persistent diary pages - keeps PaperComponent.Content, the flat string that
    /// everything else reads and that gets persisted between rounds, in sync with the leaves.
    /// </summary>
    private void SyncPages(Entity<PaperComponent> entity, PaperPagesComponent pages)
    {
        pages.CurrentPage = CurrentPageIndex(pages);
        SetPaperContent(entity, PaperPageFormat.Encode(pages.Pages));
    }

    #endregion

    #region Pirate: paperwork tags
    private void OnMacroMenuUsedMessage(Entity<PaperComponent> entity, ref PaperMacroMenuUsedMessage args)
    {
        if (args.Action != PaperAction.Write || entity.Comp.Mode != PaperAction.Write)
            return;

        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(args.Actor, ref ev);
        if (ev.Cancelled)
            return;

        _audio.PlayPvs(entity.Comp.Sound, entity);
    }

    private string ExpandPaperMacros(Entity<PaperComponent> entity, EntityUid actor, string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var author = Name(actor);
        var job = "N/A";

        if (_idCard.TryFindIdCard(actor, out var idCard))
        {
            if (!string.IsNullOrWhiteSpace(idCard.Comp.FullName))
                author = idCard.Comp.FullName;

            if (!string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
                job = idCard.Comp.LocalizedJobTitle;
        }

        var nowUtc = DateTime.UtcNow;
        var date = $"{nowUtc.Day:00}/{nowUtc.Month:00}/{StationBaseYear:0000}";
        var time = _ticker.RoundDuration().ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
        var dateTime = $"{date} {time}";
        var stationNumber = GetStationNumber(entity.Owner, actor);
        var stationCode = GetStationSecurityCode(entity.Owner, actor);

        return input
            .Replace("[author]", author)
            .Replace("[job]", job)
            .Replace("[datetime]", dateTime)
            .Replace("[date]", date)
            .Replace("[time]", time)
            .Replace("[stn]", stationNumber)
            .Replace("[code]", stationCode);
    }

    private string GetStationNumber(EntityUid paper, EntityUid actor)
    {
        var stationUid = _station.GetOwningStation(paper) ?? _station.GetOwningStation(actor) ?? GetFallbackStation(); // Pirate: paperwork tags
        if (stationUid == null)
            return "0000";

        var stationName = MetaData(stationUid.Value).EntityName;
        var codeMatch = StationCodeRegex.Match(stationName);
        if (codeMatch.Success)
            return codeMatch.Value;

        // Handles labels like "Станція: Дев" / "Station: Dev" (including no-space variants).
        var stationLabelMatch = StationLabelRegex.Match(stationName);
        if (stationLabelMatch.Success)
        {
            var labelValue = stationLabelMatch.Groups["value"].Value.Trim(',', '.', ':', ';');
            if (!string.IsNullOrWhiteSpace(labelValue))
                return labelValue;
        }

        // Fallback for station names like: "NTTG Станція Бокс"
        // or "NTTG Station Box", returning the short station name.
        var tokens = stationName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var token = tokens[i].Trim(',', '.', ':', ';');
            if (token.Equals("Станція", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Station", StringComparison.OrdinalIgnoreCase))
            {
                var fallbackName = tokens[i + 1].Trim(',', '.', ':', ';');
                if (!string.IsNullOrWhiteSpace(fallbackName))
                    return fallbackName;
            }
        }

        // Last resort: use the station value as-is (e.g. "Dev") instead of forcing 0000.
        var rawFallback = stationName.Trim(',', '.', ':', ';', ' ');
        if (!string.IsNullOrWhiteSpace(rawFallback))
            return rawFallback;

        return "0000";
    }

    #region Pirate: paperwork tags
    // Shared by GetStationNumber and GetFallbackStation so a station name that GetStationNumber can parse
    // (code, label, or the "NTTG Station Box" token form) is also recognized when picking a fallback station.
    private bool TryParseStationName(string stationName, out string value)
    {
        var codeMatch = StationCodeRegex.Match(stationName);
        if (codeMatch.Success)
        {
            value = codeMatch.Value;
            return true;
        }

        // Handles labels like "Станція: Дев" / "Station: Dev" (including no-space variants).
        var stationLabelMatch = StationLabelRegex.Match(stationName);
        if (stationLabelMatch.Success)
        {
            var labelValue = stationLabelMatch.Groups["value"].Value.Trim(',', '.', ':', ';');
            if (!string.IsNullOrWhiteSpace(labelValue))
            {
                value = labelValue;
                return true;
            }
        }

        // Fallback for station names like: "NTTG Станція Бокс"
        // or "NTTG Station Box", returning the short station name.
        var tokens = stationName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var token = tokens[i].Trim(',', '.', ':', ';');
            if (token.Equals("Станція", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Station", StringComparison.OrdinalIgnoreCase))
            {
                var fallbackName = tokens[i + 1].Trim(',', '.', ':', ';');
                if (!string.IsNullOrWhiteSpace(fallbackName))
                {
                    value = fallbackName;
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }
    #endregion

    private string GetStationSecurityCode(EntityUid paper, EntityUid actor)
    {
        var stationUid = _station.GetOwningStation(paper) ?? _station.GetOwningStation(actor) ?? GetFallbackStation(); // Pirate: paperwork tags
        if (stationUid == null)
            return Loc.GetString("alert-level-unknown");

        var ev = new PaperGetStationAlertLevelEvent();
        RaiseLocalEvent(stationUid.Value, ref ev);

        if (string.IsNullOrWhiteSpace(ev.AlertLevel))
            return Loc.GetString("alert-level-unknown");

        var alertLevelKey = $"alert-level-{ev.AlertLevel}";
        if (Loc.TryGetString(alertLevelKey, out var localizedLevel))
            return localizedLevel;

        return ev.AlertLevel;
    }

    #region Pirate: paperwork tags
    // Pirate: paperwork tags - grids not registered as station members (shuttles, debris, salvage wrecks, etc.)
    // return no owning station. Prefer the station whose name actually looks like a crewed NT station (matches
    // the same code/label patterns used above to parse [stn] from a station's own name), since a non-crew
    // station entity (arena, trade outpost, etc.) could otherwise be picked instead. If none match, give up
    // (null) and let the caller use its own placeholder rather than guessing by grid size.
    private EntityUid? GetFallbackStation()
    {
        EntityUid? match = null;
        foreach (var station in _station.GetStationsSet())
        {
            var stationName = MetaData(station).EntityName;
            if (!TryParseStationName(stationName, out _))
                continue;

            // More than one station looks like a crewed NT station: no way to tell which one actually
            // owns this paper, so give up rather than arbitrarily picking one.
            if (match != null)
                return null;

            match = station;
        }

        return match;
    }
    #endregion
    #endregion

    private void OnPaperWrite(Entity<ActivateOnPaperOpenedComponent> entity, ref PaperWriteEvent args)
    {
        _interaction.UseInHandInteraction(args.User, entity);
    }

    /// <summary>
    ///     Accepts the name and state to be stamped onto the paper, returns true if successful.
    /// </summary>
    public bool TryStamp(Entity<PaperComponent> entity, StampDisplayInfo stampInfo, string spriteStampState)
    {
        // Pirate: persistent text (diaries) - no stamping on protected paper (e.g. diaries)
        if (HasComp<NoStampingComponent>(entity))
            return false;

        if (!entity.Comp.StampedBy.Contains(stampInfo))
        {
            entity.Comp.StampedBy.Add(stampInfo);

            // Starlight-start: Clean unfilled form and signature tags when stamping to finalize the document
            var cleanedContent = CleanUnfilledTags(entity.Comp.Content);
            if (cleanedContent != entity.Comp.Content)
                SetContent(entity, cleanedContent);
            // Starlight-end

            Dirty(entity);
            if (entity.Comp.StampState == null && TryComp<AppearanceComponent>(entity, out var appearance))
            {
                entity.Comp.StampState = spriteStampState;
                // Would be nice to be able to display multiple sprites on the paper
                // but most of the existing images overlap
                _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
            }
        }
        return true;
    }

    /// <summary>
    ///     Copy any stamp information from one piece of paper to another.
    /// </summary>
    public void CopyStamps(Entity<PaperComponent?> source, Entity<PaperComponent?> target)
    {
        if (!Resolve(source, ref source.Comp) || !Resolve(target, ref target.Comp))
            return;

        target.Comp.StampedBy = new List<StampDisplayInfo>(source.Comp.StampedBy);
        target.Comp.StampState = source.Comp.StampState;
        Dirty(target);

        if (TryComp<AppearanceComponent>(target, out var appearance))
        {
            // delete any stamps if the stamp state is null
            _appearance.SetData(target, PaperVisuals.Stamp, target.Comp.StampState ?? "", appearance);
        }
    }

    public void SetContent(EntityUid entity, string content)
    {
        if (!TryComp<PaperComponent>(entity, out var paper))
            return;
        SetContent((entity, paper), content);
    }

    public void SetContent(Entity<PaperComponent> entity, string content)
    {
        // Pirate: persistent diary pages - a whole-document write (restore from the database,
        // prototype content, faxes, ...) re-derives the leaves from it.
        if (TryComp<PaperPagesComponent>(entity, out var pages))
        {
            pages.Pages = PaperPageFormat.Decode(content);
            pages.CurrentPage = Math.Clamp(pages.CurrentPage, 0, pages.Pages.Count - 1);
        }

        SetPaperContent(entity, content);
    }

    /// <summary>
    /// Pirate: persistent diary pages - the one place that actually stores a document:
    /// keeps PaperComponent.Content (the flat string everything else reads and persists)
    /// in sync and pushes the new bound UI state.
    /// </summary>
    private void SetPaperContent(Entity<PaperComponent> entity, string content)
    {
        entity.Comp.Content = content;
        Dirty(entity);
        UpdateUserInterface(entity);

        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;

        var status = string.IsNullOrWhiteSpace(content)
            ? PaperStatus.Blank
            : PaperStatus.Written;

        _appearance.SetData(entity, PaperVisuals.Status, status, appearance);
    }

    public void UpdateUserInterface(Entity<PaperComponent> entity)
    {
        // Pirate: persistent diary pages - paginated items only show the leaf on screen,
        // and its signatures stand in for the (deliberately empty) stamp list.
        var text = entity.Comp.Content;
        var stampedBy = entity.Comp.StampedBy;
        var currentPage = 0;
        var pageCount = 0;

        if (TryComp<PaperPagesComponent>(entity, out var pages))
        {
            var index = CurrentPageIndex(pages);
            currentPage = index;
            pageCount = pages.Pages.Count;
            text = pages.Pages[index].Content;
            stampedBy = pages.Pages[index].Signatures;
        }

        _uiSystem.SetUiState(entity.Owner, PaperUiKey.Key,
            new PaperBoundUserInterfaceState(text, stampedBy, entity.Comp.Mode, currentPage, pageCount));
    }

    private void OnUseInHand(Entity<PaperComponent> entity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        entity.Comp.Mode = PaperAction.Read;
        entity.Comp.WriteSessionIgnoresStampsActor = null;
        UpdateUserInterface(entity);
        _uiSystem.TryToggleUi(entity.Owner, PaperUiKey.Key, args.User);
        args.Handled = true;
    }

    # region Starlight

    private void OnSignatureRequest(Entity<PaperComponent> entity, ref PaperSignatureRequestMessage args)
    {
        var signature = GetPlayerSignature(args.Actor);
        var newText = ReplaceNthSignatureTag(entity.Comp.Content, args.SignatureIndex, signature);
        SetContent(entity, newText);

        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} signed {ToPrettyString(entity):entity} with signature: {signature}");
    }

    /// <summary>
    /// Gets the player's signature using the identity system, including rank, name, and role.
    /// </summary>
    private string GetPlayerSignature(EntityUid player)
    {
        var name = string.Empty;
        var rank = string.Empty;
        var role = string.Empty;

        // Get the identity entity (ID card, etc.)
        var identityEntity = player;
        if (TryComp<IdentityComponent>(player, out var identity)
            && identity.IdentityEntitySlot is { ContainedEntity: { } idEntity })
        {
            identityEntity = idEntity;
        }

        // Get name from identity or fallback to entity name
        name = MetaData(identityEntity).EntityName;

        // Get role from mind system
        if (TryComp<MindContainerComponent>(player, out var mindContainer) &&
            mindContainer.Mind != null)
        {
            var roleSystem = EntityManager.System<SharedRoleSystem>();
            var roleInfo = roleSystem.MindGetAllRoleInfo((mindContainer.Mind.Value, null));
            if (roleInfo.Count > 0)
            {
                role = Loc.GetString(roleInfo[0].Name);
            }
        }

        // Format: "Rank Name, Role" or fallback combinations
        var signature = string.Empty;
        if (!string.IsNullOrEmpty(rank) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role))
        {
            signature = $"{rank} {name}, {role}";
        }
        else if (!string.IsNullOrEmpty(rank) && !string.IsNullOrEmpty(name))
        {
            signature = $"{rank} {name}";
        }
        else if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role))
        {
            signature = $"{name}, {role}";
        }
        else
        {
            signature = name;
        }

        return signature;
    }

    /// <summary>
    /// Replaces the nth occurrence of [signature] tag with replacement text.
    /// </summary>
    private static string ReplaceNthSignatureTag(string text, int index, string replacement)
    {
        const string signatureTag = "[signature]";
        var currentIndex = 0;
        var pos = 0;

        while (pos < text.Length)
        {
            var foundPos = text.IndexOf(signatureTag, pos);
            if (foundPos == -1) break;

            if (currentIndex == index)
            {
                return text.Substring(0, foundPos) + replacement + text.Substring(foundPos + signatureTag.Length);
            }

            currentIndex++;
            pos = foundPos + signatureTag.Length;
        }

        return text;
    }

    /// <summary>
    /// Removes any unfilled [form] and [signature] tags, and converts [check] tags to ☐.
    /// Called when the paper is stamped to finalize the document.
    /// </summary>
    /// <param name="text">The paper text to clean</param>
    /// <returns>Text with unfilled tags cleaned</returns>
    private static string CleanUnfilledTags(string text)
    {
        return text.Replace("[form]", string.Empty)
                  .Replace("[signature]", string.Empty)
                  .Replace("[check]", "☐");
    }

    # endregion
}

/// <summary>
/// Event fired when using a pen on paper, opening the UI.
/// </summary>
[ByRefEvent]
public record struct PaperWriteEvent(EntityUid User, EntityUid Paper);

/// <summary>
/// Cancellable event for attempting to write on a piece of paper.
/// </summary>
/// <param name="paper">The paper that the writing will take place on.</param>
[ByRefEvent]
public record struct PaperWriteAttemptEvent(EntityUid Paper, string? FailReason = null, bool Cancelled = false);
