// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Server.WebUi;
using Content.Pirate.Shared.WebUi;
using Content.Shared.Access.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Server.Forensics;
using Robust.Server.GameObjects;
using Content.Server._Pirate.Photo;
using Content.Shared.Interaction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Station;
using Content.Shared.StationRecords;
using Content.Shared.CriminalRecords;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Server.RecordsBoard;

/// <summary>
///     Data for one pinned card on the evidence board. Positions are
///     grid cells (not pixels) so the server is authoritative and net
///     chatter is tiny; the page maps cells to pixels.
/// </summary>
[DataDefinition]
public sealed partial class BoardCardData
{
    [DataField] public int Id;

    /// <summary>note | char | chip | photo (only "note" ship-ready).</summary>
    [DataField("kind")] public string Kind = "note";

    [DataField] public int X;
    [DataField] public int Y;

    [DataField] public string Text = "";

    /// <summary>Phase-2: portrait wire data (data: URI) for char cards.</summary>
    [DataField("image")] public string Image = "";
}


[DataDefinition]
public sealed partial class BoardLinkData
{
    [DataField] public int Id;

    [DataField] public int A;
    [DataField] public int B;

    [DataField] public string Label = "";
}


[DataDefinition]
public sealed partial class BoardCaseData
{
    [DataField] public int Id;

    [DataField] public string Name = "";

    [DataField("cards")] public List<BoardCardData> Cards = new();

    [DataField("links")] public List<BoardLinkData> Links = new();
}

/// <summary>
///     Shared evidence board for the station records console. One
///     component instance owns every case; persisted with the station
///     map so the detective's corkboard survives until round end and is
///     shared between warden and detective (same console).
/// </summary>
[RegisterComponent]
public sealed partial class PirateEvidenceBoardComponent : Component
{
    /// <summary>Bump when the snapshot format changes; snapshot carries it.</summary>
    [DataField] public int Version = 1;

    [DataField("netWidth")] public int NetWidth = PirateEvidenceBoardSystem.BoardWidth;
    [DataField("netHeight")] public int NetHeight = PirateEvidenceBoardSystem.BoardHeight;

    [DataField("nextCardId")] public int NextCardId = 1;
    [DataField("nextCaseId")] public int NextCaseId = 1;
    [DataField("nextLinkId")] public int NextLinkId = 1;

    [DataField("currentCaseId")] public int CurrentCaseId;

    [DataField("cases")] public List<BoardCaseData> Cases = new();
}

/// <summary>
///     Server half of the evidence board. The client page performs no
///     decisions: every op (add/move/del/link/case changes) comes in
///     through EvidenceBoardRequestEvent, the authoritative state lives
///     in PirateEvidenceBoardComponent, and every mutation broadcasts a
///     fresh snapshot so both detectives see the same board.
/// </summary>
public sealed class PirateEvidenceBoardSystem : EntitySystem
{
    public const int BoardWidth = 32;
    public const int BoardHeight = 18;

    public const int MaxCardsPerCase = 64;
    public const int MaxCases = 12;
    public const int MaxTextLength = 1200;

    /// <summary>Photo previews larger than this ship text-only (snapshot budget).</summary>
    public const int MaxImageBytes = 128_000;

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStationSystem _stationSystem = default!;
    [Dependency] private readonly SharedStationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public const float FileRange = 2.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EvidenceBoardRequestEvent>(OnRequest);
        // Paper intake, two directions (both reach FilePaper):
        //  1. verb on the paper itself ("file this sheet")
        //  2. verb on the console ("file the paper in my hand") — the
        //     click-the-console flow people actually try first.
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<InteractionVerb>>(OnPaperVerbs);
        SubscribeLocalEvent<PirateEvidenceBoardComponent, GetVerbsEvent<InteractionVerb>>(OnConsoleVerbs);
        SubscribeLocalEvent<ForensicPadComponent, GetVerbsEvent<InteractionVerb>>(OnPadVerbs);
        SubscribeLocalEvent<PhotoCardComponent, GetVerbsEvent<InteractionVerb>>(OnPhotoVerbs);
    }

    /// <summary>Fingerprint pad sample (not paper): file as a chip.</summary>
    private void OnPadVerbs(Entity<ForensicPadComponent> pad, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;
        var console = FindNearbyConsole(args.User);
        if (console == null)
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => FilePad(pad, console.Value, user),
            Text = Loc.GetString("pirate-evidence-board-file-verb"),
            Priority = 2,
        });
    }

    private void FilePad(Entity<ForensicPadComponent> pad, Entity<PirateEvidenceBoardComponent> console, EntityUid user)
    {
        var board = new Entity<PirateEvidenceBoardComponent>(console.Owner, console.Comp);
        EnsureInitialized(board);
        var caseData = CurrentCase(board);
        if (caseData == null || caseData.Cards.Count >= MaxCardsPerCase)
            return;
        var sample = pad.Comp.Sample?.Trim() ?? "";
        if (sample.Length == 0)
        {
            Popup(user, Loc.GetString("pirate-evidence-board-blank"));
            return;
        }
        if (!TryFindFreeSpot(board.Comp, caseData, out var cx, out var cy))
        {
            Popup(user, Loc.GetString("pirate-evidence-board-nospace"));
            return;
        }
        var name = _entMan.GetComponent<MetaDataComponent>(pad.Owner).EntityName ?? "pad";
        caseData.Cards.Add(new BoardCardData
        {
            Id = board.Comp.NextCardId++,
            Kind = "chip",
            X = cx,
            Y = cy,
            Text = Trim($"[{name}]\nfingerprints: {sample}"),
        });
        Dirty(board.Owner, board.Comp);
        BroadcastState(board, GetNetEntity(console.Owner));
        Popup(user, Loc.GetString("pirate-evidence-board-filed", ("name", name)));
    }

    /// <summary>Photo card: PNG preview goes straight onto the card.</summary>
    private void OnPhotoVerbs(Entity<PhotoCardComponent> photo, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;
        var console = FindNearbyConsole(args.User);
        if (console == null)
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => FilePhoto(photo, console.Value, user),
            Text = Loc.GetString("pirate-evidence-board-file-verb"),
            Priority = 2,
        });
    }

    private void FilePhoto(Entity<PhotoCardComponent> photo, Entity<PirateEvidenceBoardComponent> console, EntityUid user)
    {
        var board = new Entity<PirateEvidenceBoardComponent>(console.Owner, console.Comp);
        EnsureInitialized(board);
        var caseData = CurrentCase(board);
        if (caseData == null || caseData.Cards.Count >= MaxCardsPerCase)
            return;
        if (!TryFindFreeSpot(board.Comp, caseData, out var cx, out var cy))
        {
            Popup(user, Loc.GetString("pirate-evidence-board-nospace"));
            return;
        }
        var comp = board.Comp;
        var name = _entMan.GetComponent<MetaDataComponent>(photo.Owner).EntityName ?? "photo";
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(photo.Comp.CustomDescription))
            lines.Add(photo.Comp.CustomDescription!);
        if (!string.IsNullOrWhiteSpace(photo.Comp.Caption))
            lines.Add(photo.Comp.Caption!);
        if (!string.IsNullOrWhiteSpace(photo.Comp.BaseDescription))
            lines.Add(photo.Comp.BaseDescription!);

        var image = "";
        var preview = RenderSanePreview(photo.Comp.PreviewData, photo.Comp.ImageData);
        if (preview != null)
            image = "data:image/png;base64," + Convert.ToBase64String(preview);

        caseData.Cards.Add(new BoardCardData
        {
            Id = comp.NextCardId++,
            Kind = "photo",
            X = cx,
            Y = cy,
            Text = Trim($"[{name}]\n" + string.Join("\n", lines)),
            Image = image,
        });
        Dirty(board.Owner, board.Comp);
        BroadcastState(board, GetNetEntity(console.Owner));
        Popup(user, Loc.GetString("pirate-evidence-board-filed", ("name", name)));
    }

    private void OnConsoleVerbs(Entity<PirateEvidenceBoardComponent> console, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;
        if (!_access.IsAllowed(args.User, console.Owner))
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => FileHeldPaper(console, user),
            Text = Loc.GetString("pirate-evidence-board-file-held"),
            Priority = 2,
        });
    }

    private void FileHeldPaper(Entity<PirateEvidenceBoardComponent> console, EntityUid user)
    {
        if (_hands.GetActiveItem(user) is not { } item ||
            !TryComp<PaperComponent>(item, out var paper))
        {
            Popup(user, Loc.GetString("pirate-evidence-board-hold-paper"));
            return;
        }
        FilePaper((item, paper), console, user);
    }

    private void OnPaperVerbs(Entity<PaperComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;
        var console = FindNearbyConsole(args.User);
        if (console == null)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => FilePaper(ent, console.Value, user),
            Text = Loc.GetString("pirate-evidence-board-file-verb"),
            Priority = 2,
        });
    }

    /// <summary>The nearest records console with a board in reach that the user can unlock.</summary>
    private Entity<PirateEvidenceBoardComponent>? FindNearbyConsole(EntityUid user)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange<PirateEvidenceBoardComponent>(
                     Transform(user).Coordinates, FileRange))
        {
            if (!_access.IsAllowed(user, candidate.Owner))
                continue;
            return candidate;
        }
        return null;
    }

    private void OnRequest(EvidenceBoardRequestEvent msg, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(msg.Console, out var console) || !Exists(console.Value) ||
            !TryComp(console.Value, out PirateEvidenceBoardComponent? comp))
            return;

        var board = new Entity<PirateEvidenceBoardComponent>(console.Value, comp);
        EnsureInitialized(board);

        switch (msg.Action)
        {
            case "sync":
                RaiseNetworkEvent(new EvidenceBoardStateEvent
                {
                    Console = msg.Console,
                    Snapshot = BuildSnapshot(board),
                }, args.SenderSession.Channel);
                return;
            case "addnote":
                OpAddNote(board, msg.Data);
                break;
            case "pinchar":
                OpPinChar(board, msg.Data, msg, args.SenderSession.Channel);
                break;
            case "portrait":
                OpPortrait(board, msg);
                break;
            case "settext":
                OpSetText(board, msg.Data);
                break;
            case "move":
                OpMove(board, msg.Data);
                break;
            case "del":
                OpDelete(board, msg.Data);
                break;
            case "link":
                OpLink(board, msg.Data);
                break;
            case "unlink":
                OpUnlink(board, msg.Data);
                break;
            case "newcase":
                OpNewCase(board, msg.Data);
                break;
            case "delcase":
                OpDeleteCase(board, msg.Data);
                break;
            case "switchcase":
                OpSwitchCase(board, msg.Data);
                break;
            case "renamecase":
                OpRenameCase(board, msg.Data);
                break;
            case "printcase":
                OpPrintCase(board, args.SenderSession);
                return;
            default:
                Logger.DebugS("webui.board", $"unknown action {msg.Action} on {msg.Console}");
                return;
        }

        Dirty(board.Owner, board.Comp);
        BroadcastState(board, msg.Console);
    }

    // ---- ops (validate + mutate; page keeps positions clamped server-side) ----

    private void OpAddNote(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "cx|cy|text" (cx/cy are top-left cells, page already clamped).
        var parts = data.Split('|', 3);
        if (parts.Length < 3)
            return;
        if (!int.TryParse(parts[0], out var cx) || !int.TryParse(parts[1], out var cy))
            return;

        var comp = board.Comp;
        var caseData = CurrentCase(board);
        if (caseData == null || caseData.Cards.Count >= MaxCardsPerCase)
            return;

        (cx, cy) = ClampCardPos(comp, cx, cy);
        caseData.Cards.Add(new BoardCardData
        {
            Id = comp.NextCardId++,
            Kind = "note",
            X = cx,
            Y = cy,
            Text = Trim(parts[2]),
        });
    }

    /// <summary>
    ///     Character card pinned from the records console UI. Payload:
    ///     "cx|cy|name<US>job<US>age<US>species<US>gender<US>prints<US>dna"
    ///     (US = \u001f unit separator). cx/cy = -1/-1 means server picks a
    ///     free patch, which is what pin-from-records uses.
    /// </summary>
    private void OpPinChar(Entity<PirateEvidenceBoardComponent> board, string data, EvidenceBoardRequestEvent msg, INetChannel channel)
    {
        var parts = data.Split('|', 3);
        if (parts.Length < 3)
            return;
        if (!int.TryParse(parts[0], out var cx) || !int.TryParse(parts[1], out var cy))
            return;

        var comp = board.Comp;
        var caseData = CurrentCase(board);
        if (caseData == null || caseData.Cards.Count >= MaxCardsPerCase)
            return;

        var text = parts[2].Trim();
        if (text.Length == 0)
            return;

        if (cx < 0 || cy < 0)
        {
            if (!TryFindFreeSpot(comp, caseData, out cx, out cy))
                return; // board truly full: page falls back to manual placement
        }
        else
        {
            (cx, cy) = ClampCardPos(comp, cx, cy);
        }

        caseData.Cards.Add(new BoardCardData
        {
            Id = comp.NextCardId++,
            Kind = "char",
            X = cx,
            Y = cy,
            Text = Trim(text),
        });

        // Portrait hunt: the criminal-records copy of the crew record
        // stores the spawn-time HumanoidCharacterProfile + job. If the
        // criminal console already rendered a photo, reuse it; otherwise
        // quest the pinning client to render the profile (dummy + Export).
        TryAttachPortrait(board, msg, caseData.Cards[^1].Id, channel);
    }

    /// <summary>Client rendered a spawn-time portrait: data:URI the PNG
    /// and paint the card (capped by the usual snapshot budget).</summary>
    private void OpPortrait(Entity<PirateEvidenceBoardComponent> board, EvidenceBoardRequestEvent msg)
    {
        if (msg.Image is not { Length: > 32 })
            return;
        if (!int.TryParse(msg.Data, out var cardId))
            return;
        // Duplicate-render replies (client retries):
        foreach (var c in board.Comp.Cases)
        {
            if (c.Cards.Find(x => x.Id == cardId)?.Image.Length > 0)
                return;
        }
        var preview = RenderSanePreview(msg.Image, null);
        if (preview == null || preview.Length == 0)
            return;
        PaintPortrait(board, cardId, "data:image/png;base64," + Convert.ToBase64String(preview));
    }

    /// <summary>True if a portrait got attached or a client was queued.</summary>
    private void TryAttachPortrait(
        Entity<PirateEvidenceBoardComponent> board,
        EvidenceBoardRequestEvent msg,
        int cardId,
        INetChannel questTo)
    {
        try
        {
            if (msg.RecordKey <= 0)
                return;
            var station = _stationSystem.GetOwningStation(board.Owner);
            if (station == null)
                return;
            var key = new StationRecordKey(msg.RecordKey, station.Value);

            if (!_stationRecords.TryGetRecord(key, out CriminalRecord? cr) || cr == null)
                return;

            if (cr.PortraitPreviewData is { Length: > 400 })
            {
                var uri = "data:image/png;base64," + Convert.ToBase64String(cr.PortraitPreviewData);
                PaintPortrait(board, cardId, uri);
                return;
            }

            if (cr.PortraitProfileSnapshot == null)
                return;

            RaiseNetworkEvent(new EvidenceBoardPortraitQuestEvent
            {
                Console = msg.Console,
                CardId = cardId,
                RecordKey = msg.RecordKey,
                Profile = cr.PortraitProfileSnapshot,
                JobProto = cr.GeneralRecordSnapshot?.JobPrototype ?? "",
            }, questTo);
        }
        catch (Exception ex)
        {
            Logger.WarningS("webui.board", $"portrait lookup failed: {ex.Message}");
        }
    }

    /// <summary>Store the data URI on a card and broadcast.</summary>
    private void PaintPortrait(Entity<PirateEvidenceBoardComponent> board, int cardId, string dataUri)
    {
        foreach (var c in board.Comp.Cases)
        {
            var card = c.Cards.Find(x => x.Id == cardId);
            if (card == null)
                continue;
            card.Image = dataUri;
            Dirty(board.Owner, board.Comp);
            BroadcastState(board, GetNetEntity(board.Owner));
            return;
        }
    }

    /// <summary>First 5x4 patch in the current case that no card overlaps.</summary>
    private static bool TryFindFreeSpot(
        PirateEvidenceBoardComponent comp, BoardCaseData caseData, out int outX, out int outY)
    {
        const int cardW = 5;
        const int cardH = 4;
        for (var y = 0; y <= comp.NetHeight - cardH; y++)
        {
            for (var x = 0; x <= comp.NetWidth - cardW; x++)
            {
                var free = true;
                foreach (var card in caseData.Cards)
                {
                    if (card.X < x + cardW && x < card.X + cardW &&
                        card.Y < y + cardH && y < card.Y + cardH)
                    {
                        free = false;
                        break;
                    }
                }
                if (free)
                {
                    outX = x;
                    outY = y;
                    return true;
                }
            }
        }
        outX = 0;
        outY = 0;
        return false;
    }

    private void OpSetText(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "cardId|text"
        var parts = data.Split('|', 2);
        if (parts.Length < 2 || !int.TryParse(parts[0], out var id))
            return;
        var card = FindCard(board, id);
        if (card != null)
            card.Text = Trim(parts[1]);
    }

    private void OpMove(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "cardId|cx|cy"
        var parts = data.Split('|', 3);
        if (parts.Length < 3 || !int.TryParse(parts[0], out var id))
            return;
        if (!int.TryParse(parts[1], out var cx) || !int.TryParse(parts[2], out var cy))
            return;
        var card = FindCard(board, id);
        if (card != null)
            (card.X, card.Y) = ClampCardPos(board.Comp, cx, cy);
    }

    private void OpDelete(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "cardId"
        if (!int.TryParse(Trim(data), out var id))
            return;
        var caseData = CurrentCase(board);
        if (caseData == null)
            return;
        caseData.Cards.RemoveAll(c => c.Id == id);
        caseData.Links.RemoveAll(l => l.A == id || l.B == id);
    }

    private void OpLink(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "a|b[|label]"
        var parts = data.Split('|', 3);
        if (parts.Length < 2 || !int.TryParse(parts[0], out var a) || !int.TryParse(parts[1], out var b))
            return;
        var caseData = CurrentCase(board);
        if (caseData == null || a == b)
            return;
        if (FindCard(board, a) == null || FindCard(board, b) == null)
            return;
        foreach (var link in caseData.Links)
        {
            if ((link.A == a && link.B == b) || (link.A == b && link.B == a))
                return;
        }
        if (caseData.Links.Count >= MaxCardsPerCase)
            return;
        var label = parts.Length >= 3 ? Trim(parts[2]) : "";
        caseData.Links.Add(new BoardLinkData { Id = board.Comp.NextLinkId++, A = a, B = b, Label = label });
    }

    private void OpUnlink(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "linkId"? no wait — currently link clicks carry the link id.
        if (!int.TryParse(Trim(data), out var id))
            return;
        CurrentCase(board)?.Links.RemoveAll(l => l.Id == id);
    }

    private void OpNewCase(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        var comp = board.Comp;
        if (comp.Cases.Count >= MaxCases)
            return;
        var name = Trim(data);
        if (name.Length == 0)
            name = $"Case {comp.NextCaseId}";
        comp.Cases.Add(new BoardCaseData { Id = comp.NextCaseId++, Name = name });
        comp.CurrentCaseId = comp.Cases[^1].Id;
    }

    private void OpDeleteCase(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        var comp = board.Comp;
        if (!int.TryParse(Trim(data), out var id))
            return;
        comp.Cases.RemoveAll(c => c.Id == id);
        if (comp.CurrentCaseId == id)
            comp.CurrentCaseId = comp.Cases.Count > 0 ? comp.Cases[0].Id : 0;
    }

    private void OpSwitchCase(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        if (!int.TryParse(Trim(data), out var id))
            return;
        foreach (var c in board.Comp.Cases)
        {
            if (c.Id == id)
                board.Comp.CurrentCaseId = id;
        }
    }

    private void OpRenameCase(Entity<PirateEvidenceBoardComponent> board, string data)
    {
        // "caseId|name"
        var parts = data.Split('|', 2);
        if (parts.Length < 2 || !int.TryParse(parts[0], out var id))
            return;
        foreach (var c in board.Comp.Cases)
        {
            if (c.Id == id)
                c.Name = Trim(parts[1]);
        }
    }

    /// <summary>
    ///     File a paper (regular note, forensic scanner report, log probe
    ///     printout, fingerprint/blood result — anything with
    ///     PaperComponent) into the current case of the nearest records
    ///     console: markup is stripped, the paper's name heads the chip.
    /// </summary>
    public void FilePaper(Entity<PaperComponent> paper, Entity<PirateEvidenceBoardComponent> console, EntityUid user)
    {
        var board = new Entity<PirateEvidenceBoardComponent>(console.Owner, console.Comp);
        EnsureInitialized(board);
        var caseData = CurrentCase(board);
        if (caseData == null || caseData.Cards.Count >= MaxCardsPerCase)
            return;

        var plain = StripMarkup(paper.Comp.Content);
        if (string.IsNullOrWhiteSpace(plain))
        {
            Popup(user, Loc.GetString("pirate-evidence-board-blank"));
            return;
        }
        if (!TryFindFreeSpot(board.Comp, caseData, out var cx, out var cy))
        {
            Popup(user, Loc.GetString("pirate-evidence-board-nospace"));
            return;
        }

        var comp = board.Comp;
        var name = _entMan.GetComponent<MetaDataComponent>(paper.Owner).EntityName ?? "paper";
        caseData.Cards.Add(new BoardCardData
        {
            Id = comp.NextCardId++,
            Kind = "chip",
            X = cx,
            Y = cy,
            Text = Trim($"[{name}]\n{plain}"),
        });
        Dirty(board.Owner, board.Comp);
        BroadcastState(board, GetNetEntity(console.Owner));
        Popup(user, Loc.GetString("pirate-evidence-board-filed", ("name", name)));
    }

    /// <summary>Light markup clean: strip <...> tags, cap blank lines.</summary>
    private static string StripMarkup(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";
        // Paper markup appears both as HTML-ish tags and bracketed
        // characters ([color=red], [/bold]...): strip both, keep regular
        // bracketed words (our [name] header is added after this).
        var noTags = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]*>", "");
        noTags = System.Text.RegularExpressions.Regex.Replace(
            noTags, @"\[/?[a-z\-]+(=[^\]]*)?\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var lines = noTags.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd();
        var sb = new System.Text.StringBuilder();
        var blanks = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                blanks++;
                if (blanks > 1)
                    continue;
            }
            else
            {
                blanks = 0;
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString().Trim();
    }

    private void Popup(EntityUid user, string text)
    {
        _popup.PopupEntity(text, user, user, PopupType.Medium);
    }

    /// <summary>
    ///     Photo previews are sometimes the system's 8x8 fallback PNG
    ///     (PreviewSize = 8 in the photo system) — upscaling those looks
    ///     like a pixel mush. When the stored preview is that tiny, build
    ///     our own from the full PNG with ImageSharp instead.
    /// </summary>
    private static byte[]? RenderSanePreview(byte[]? previewData, byte[]? imageData)
    {
        // A real preview is at least a few hundred bytes; 8x8 PNGs compress
        // smaller than that.
        if (previewData is { Length: > 400 })
            return previewData;
        if (imageData is not { Length: > 0 })
            return null;
        try
        {
            using var src = new MemoryStream(imageData, writable: false);
            using var origin = Image.Load<Rgba32>(src);
            foreach (var max in new[] { 320, 192 })
            {
                using var outStream = new MemoryStream();
                using (var scaled = origin.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(max, max),
                    Mode = ResizeMode.Max,
                })))
                {
                    scaled.SaveAsPng(outStream);
                }
                if (outStream.Length <= MaxImageBytes)
                    return outStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            Logger.WarningS("webui.board", $"photo preview render failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    ///     Print the current case as a paper document: numbered report
    ///     blocks per card + connections listing. Spawns at the printer
    ///     user's feet and tries to pick it up.
    /// </summary>
    private void OpPrintCase(Entity<PirateEvidenceBoardComponent> board, ICommonSession? session)
    {
        EnsureInitialized(board);
        var caseData = CurrentCase(board);
        if (caseData == null)
            return;
        var user = session?.AttachedEntity;
        if (user == null || !_entMan.EntityExists(user.Value))
            return;

        var sb = new System.Text.StringBuilder();
        sb.Append("[bold]Evidence Report[/bold]\n");
        sb.Append("[bold]Case:[/bold] ").Append(CleanPrintLine(caseData.Name)).Append("\n\n");

        var names = new Dictionary<int, string>();
        foreach (var card in caseData.Cards)
        {
            var f = card.Text.Split('\u001f');
            var tag = caseData.Cards.IndexOf(card) + 1;
            switch (card.Kind)
            {
                case "char":
                {
                    var name = f.Length > 0 && f[0].Trim().Length > 0 ? f[0].Trim() : "unnamed person";
                    names[card.Id] = name;
                    sb.Append("[bold]").Append(tag).Append(". ").Append(CleanPrintLine(name));
                    if (f.Length > 1 && f[1].Trim().Length > 0)
                        sb.Append(" — ").Append(CleanPrintLine(f[1]));
                    sb.Append("[/bold]\n");
                    if (f.Length > 3 && f[3].Trim().Length > 0)
                        sb.Append("[italic]").Append(CleanPrintLine(f[3])).Append("[/italic]\n");
                    var hasDna = f.Length > 6 && f[6].Trim().Length > 0;
                    if (hasDna || (f.Length > 5 && f[5].Trim().Length > 0))
                    {
                        sb.Append("  prints: ").Append(f.Length > 5 && f[5].Trim().Length > 0 ? CleanPrintLine(f[5]) : "none recorded").Append('\n');
                        sb.Append("  DNA: ").Append(hasDna ? CleanPrintLine(f[6]) : "none recorded").Append('\n');
                    }
                    sb.Append('\n');
                    break;
                }
                case "photo":
                {
                    var lines = card.Text.Split('\n');
                    var baseName = lines.Length > 0 ? CleanPrintLine(lines[0].Trim().Trim('[').Trim(']')) : "photo";
                    names[card.Id] = "photo: " + baseName;
                    sb.Append("[bold]").Append(tag).Append(". Photo: ").Append(baseName)
                        .Append("[/bold]\n");
                    var caption = string.Join(" ", lines.Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0));
                    if (caption.Length > 400)
                        caption = caption.Substring(0, 400) + "…";
                    if (caption.Length > 0)
                        sb.Append(CleanPrintLine(caption)).Append('\n');
                    sb.Append('\n');
                    break;
                }
                default:
                {
                    var lines = card.Text.Split('\n');
                    var title = lines.Length > 0 ? CleanPrintLine(lines[0].Trim().Trim('[').Trim(']')) : "paper";
                    if (title.Length == 0)
                        title = "note";
                    names[card.Id] = title;
                    sb.Append("[bold]").Append(tag).Append(". ").Append(title).Append("[/bold]\n");
                    var body = string.Join(" ", lines.Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0)).Trim();
                    if (body.Length == 0)
                        body = string.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0));
                    body = CleanPrintLine(body);
                    if (body.Length > 400)
                        body = body.Substring(0, 400) + "…";
                    if (body.Length > 0)
                        sb.Append(body).Append('\n');
                    sb.Append('\n');
                    break;
                }
            }
        }

        if (caseData.Links.Count > 0)
        {
            sb.Append("\n[bold]Connections[/bold]\n");
            foreach (var link in caseData.Links)
            {
                if (!names.TryGetValue(link.A, out var a) || !names.TryGetValue(link.B, out var b))
                    continue;
                sb.Append(CleanPrintLine(a)).Append(" ─ ");
                if (link.Label.Length > 0)
                    sb.Append(CleanPrintLine(link.Label)).Append(" ─ ");
                sb.Append(CleanPrintLine(b)).Append("\n");
            }
        }

        var paper = _entMan.SpawnEntity("Paper", _xform.GetMoverCoordinates(user.Value));
        var paperComp = _entMan.GetComponent<PaperComponent>(paper);
        paperComp.Content = sb.ToString();
        _meta.SetEntityName(paper, Loc.GetString("pirate-evidence-report-name", ("case", caseData.Name)));
        _hands.TryPickupAnyHand(user.Value, paper);
        Popup(user.Value, Loc.GetString("pirate-evidence-board-printed"));
    }

    /// <summary>Print-safe scrub: brackets become parentheses (paper markup
    /// would otherwise eat them), tag leftovers are matches, whitespace
    /// collapses to single spaces.</summary>
    private static string CleanPrintLine(string s)
    {
        s = s.Replace('[', '(').Replace(']', ')');
        var cleaned = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ").Trim();
        if (cleaned.Length > 200)
            cleaned = cleaned.Substring(0, 200) + "…";
        return cleaned;
    }

    // ---- helpers ----

    private void EnsureInitialized(Entity<PirateEvidenceBoardComponent> board)
    {
        if (board.Comp.Cases.Count > 0 && board.Comp.CurrentCaseId > 0)
        {
            foreach (var c in board.Comp.Cases)
            {
                if (c.Id == board.Comp.CurrentCaseId)
                    return;
            }
        }
        if (board.Comp.Cases.Count == 0)
        {
            board.Comp.Cases.Add(new BoardCaseData
            {
                Id = board.Comp.NextCaseId++,
                Name = "Case 1",
            });
        }
        board.Comp.CurrentCaseId = board.Comp.Cases[0].Id;
        Dirty(board.Owner, board.Comp);
    }

    private BoardCaseData? CurrentCase(Entity<PirateEvidenceBoardComponent> board)
    {
        foreach (var c in board.Comp.Cases)
        {
            if (c.Id == board.Comp.CurrentCaseId)
                return c;
        }
        return null;
    }

    private BoardCardData? FindCard(Entity<PirateEvidenceBoardComponent> board, int id)
    {
        return CurrentCase(board)?.Cards.Find(c => c.Id == id);
    }

    private (int X, int Y) ClampCardPos(PirateEvidenceBoardComponent comp, int x, int y)
    {
        const int cardW = 5;
        const int cardH = 4;
        x = Math.Clamp(x, 0, Math.Max(0, comp.NetWidth - cardW));
        y = Math.Clamp(y, 0, Math.Max(0, comp.NetHeight - cardH));
        return (x, y);
    }

    private static string Trim(string s)
    {
        s = s.Trim();
        if (s.Length > MaxTextLength)
            s = s.Substring(0, MaxTextLength);
        return s;
    }

    private void BroadcastState(Entity<PirateEvidenceBoardComponent> board, NetEntity netConsole)
    {
        // Broadcast (the board is shared between warden + detective): open
        // pages pull/apply it; everyone else ignores.
        RaiseNetworkEvent(new EvidenceBoardStateEvent
        {
            Console = netConsole,
            Snapshot = BuildSnapshot(board),
        });
    }

    private void PushStateTo(Entity<PirateEvidenceBoardComponent> board, NetEntity netConsole, INetChannel channel)
    {
        RaiseNetworkEvent(new EvidenceBoardStateEvent
        {
            Console = netConsole,
            Snapshot = BuildSnapshot(board),
        }, channel);
    }

    /// <summary>
    ///     Full board snapshot, hand-built JSON (sandbox: no
    ///     System.Text.Json in content assemblies).
    ///     {"v","theme","current","cases":[{"id","name","cards":[...],"links":[...]}]}
    /// </summary>
    private string BuildSnapshot(Entity<PirateEvidenceBoardComponent> board)
    {
        var theme = PirateWebThemeResolver.ThemeOf(_entMan, _prototypes, board.Owner);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"v\":1,\"theme\":").Append(Quoted(theme));
        sb.Append(",\"w\":").Append(board.Comp.NetWidth);
        sb.Append(",\"h\":").Append(board.Comp.NetHeight);
        sb.Append(",\"current\":").Append(board.Comp.CurrentCaseId);
        sb.Append(",\"cases\":[");
        var firstCase = true;
        foreach (var c in board.Comp.Cases)
        {
            if (!firstCase)
                sb.Append(',');
            firstCase = false;
            sb.Append("{\"id\":").Append(c.Id);
            sb.Append(",\"name\":").Append(Quoted(c.Name));
            sb.Append(",\"cards\":[");
            var first = true;
            foreach (var card in c.Cards)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(card.Id);
                sb.Append(",\"kind\":").Append(Quoted(card.Kind));
                sb.Append(",\"x\":").Append(card.X);
                sb.Append(",\"y\":").Append(card.Y);
                sb.Append(",\"text\":").Append(Quoted(card.Text));
                sb.Append(",\"img\":").Append(Quoted(card.Image));
                sb.Append('}');
            }
            sb.Append("],\"links\":[");
            first = true;
            foreach (var link in c.Links)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(link.Id);
                sb.Append(",\"a\":").Append(link.A);
                sb.Append(",\"b\":").Append(link.B);
                sb.Append(",\"label\":").Append(Quoted(link.Label));
                sb.Append('}');
            }
            sb.Append("]}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    /// <summary>JSON string escape; mirrors WebUiSpikeBridge.JsonString.</summary>
    private static string Quoted(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ')
                        sb.Append("\\u").Append(((int) ch).ToString("x4"));
                    else
                        sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
