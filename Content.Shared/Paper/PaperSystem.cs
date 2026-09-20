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
using Robust.Shared.Audio.Systems;
using static Content.Shared.Paper.PaperComponent;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
// Starlight-start
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
// Starlight-end

#region Pirate: paperwork tags
using Robust.Shared.Network;
using System.Globalization;
using System.Text.RegularExpressions;
using Content.Shared._Pirate.Paper;
using Content.Shared._Pirate.PersistentText;
using Content.Shared.Access.Systems;
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

    private static readonly ProtoId<TagPrototype> WriteIgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";
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

        SubscribeLocalEvent<NoStampingComponent, BeforeStampEvent>(OnBeforeStamp); // Pirate: persistent text (diaries)

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
        // only allow editing if there are no stamps or when using a cyberpen
        var editable = entity.Comp.StampedBy.Count == 0 || _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag);
        if (_tagSystem.HasTag(args.Used, WriteTag))
        {
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
        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(args.Actor, ref ev);
        if (ev.Cancelled)
            return;

        // Pirate: persistent text (diaries) - only the bound owner may write;
        // unbound items bind to the first writer (e.g. spawned outside a loadout).
        if (TryComp<PersistentTextComponent>(entity, out var persistentText))
        {
            if (!CanWrite(entity.Owner, persistentText, args.Actor))
            {
                _popupSystem.PopupClient(Loc.GetString("persistent-text-cant-write"), entity.Owner, args.Actor);
                return;
            }

            BindPersistentTextOwner(entity.Owner, persistentText, args.Actor);
        }

        var processedText = ExpandPaperMacros(entity, args.Actor, args.Text); // Pirate: paperwork tags

        if (processedText.Length <= entity.Comp.ContentSize) // Pirate: paperwork tags
        {
            SetContent(entity, processedText); // Pirate: paperwork tags

            var paperStatus = string.IsNullOrWhiteSpace(processedText) ? PaperStatus.Blank : PaperStatus.Written; // Pirate: paperwork tags

            if (TryComp<AppearanceComponent>(entity, out var appearance))
                _appearance.SetData(entity, PaperVisuals.Status, paperStatus, appearance);

            if (TryComp(entity, out MetaDataComponent? meta))
                _metaSystem.SetEntityDescription(entity, "", meta);

            _adminLogger.Add(LogType.Chat,
                LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} has written on {ToPrettyString(entity):entity} the following text: {processedText}"); // Pirate: paperwork tags

            _audio.PlayPvs(entity.Comp.Sound, entity);
        }

        entity.Comp.Mode = PaperAction.Read;
        UpdateUserInterface(entity);
    }

    #region Pirate: persistent text (diaries)

    private void OnBeforeStamp(Entity<NoStampingComponent> entity, ref BeforeStampEvent args)
    {
        // A stamp would make the paper uneditable; diaries must stay writable.
        args.Cancelled = true;
    }

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
    public bool TryStamp(Entity<PaperComponent> entity, StampDisplayInfo stampInfo, string spriteStampState, EntityUid? user = null) // Pirate: persistent text (diaries) - user param
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
        _uiSystem.SetUiState(entity.Owner, PaperUiKey.Key, new PaperBoundUserInterfaceState(entity.Comp.Content, entity.Comp.StampedBy, entity.Comp.Mode));
    }

    private void OnUseInHand(Entity<PaperComponent> entity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        entity.Comp.Mode = PaperAction.Read;
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
