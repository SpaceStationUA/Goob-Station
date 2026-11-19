// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Crayon;

public sealed partial class CrayonLibrarySystem
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;

    private const int PageSize = 6;

    private void InitializeModeration()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) || !_admins.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;

        var target = args.Target;
        EntityUid? stamp = null;
        NetUserId? owner;
        var title = Name(target);
        if (TryComp<CrayonStampComponent>(target, out var drawing))
        {
            stamp = target;
            owner = drawing.Artist;
            title = drawing.ArtistName ?? title;
            if (owner is { } account)
            {
                var username = _players.TryGetPlayerData(account, out var data) ? data.UserName : account.ToString();
                title = $"{title} ({username})";
            }
        }
        else if (TryComp<ActorComponent>(target, out var player))
        {
            owner = player.PlayerSession.UserId;
            title = player.PlayerSession.Name;
        }
        else if (_minds.TryGetMind(target, out _, out var mind) && (mind.UserId ?? mind.OriginalOwnerUserId) is { } id)
            owner = id;
        else
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("pirate-crayon-admin-verb"),
            Category = VerbCategory.Admin,
            Impact = LogImpact.Low,
            Act = () =>
            {
                if (_admins.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
                    _eui.OpenEui(new CrayonAdminEui(owner, title, stamp), actor.PlayerSession);
            },
        });
    }

    public CrayonAdminEuiState GetPage(NetUserId? owner, string title, EntityUid? single, bool placed, int page, string? status)
    {
        var rows = new List<CrayonAdminRow>();
        var pages = 1;
        if (single is { } uid)
        {
            if (!TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) && TryComp<CrayonStampComponent>(uid, out var stamp))
                rows.Add(StampRow(uid, stamp));
            page = 0;
        }
        else if (placed)
        {
            var stamps = new List<Entity<CrayonStampComponent>>();
            var query = EntityQueryEnumerator<CrayonStampComponent>();
            while (query.MoveNext(out var id, out var stamp))
            {
                if (owner != null && stamp.Artist == owner && !TerminatingOrDeleted(id) && !EntityManager.IsQueuedForDeletion(id))
                    stamps.Add((id, stamp));
            }

            pages = PageCount(stamps.Count);
            page = Math.Clamp(page, 0, pages - 1);
            foreach (var stamp in stamps.OrderByDescending(s => s.Owner.Id).Skip(page * PageSize).Take(PageSize))
                rows.Add(StampRow(stamp.Owner, stamp.Comp));
        }
        else if (owner is { } account)
        {
            var saved = GetDrawings(account);
            if (saved == null)
                status = "pirate-crayon-library-unavailable";
            else
            {
                pages = PageCount(saved.Count);
                page = Math.Clamp(page, 0, pages - 1);
                foreach (var d in saved.Skip(page * PageSize).Take(PageSize))
                    rows.Add(new CrayonAdminRow(d.Id, null, d.Name, d.Symbol));
            }
        }

        return new CrayonAdminEuiState(title, placed, single != null, page, pages, rows, status);
    }

    private static int PageCount(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);

    private CrayonAdminRow StampRow(EntityUid uid, CrayonStampComponent stamp)
    {
        var coordinates = Transform(uid).Coordinates;
        var title = $"{stamp.DrawingName ?? Name(uid)} · {coordinates}";
        return new CrayonAdminRow(null, GetNetEntity(uid), title, stamp.Symbol?.IsValid() == true ? stamp.Symbol : null);
    }

    public string Moderate(ICommonSession admin, NetUserId? owner, EntityUid? single, CrayonAdminAction action)
    {
        if (!_admins.HasAdminFlag(admin, AdminFlags.Admin))
            return "pirate-crayon-library-changed";

        CrayonSymbol symbol;
        string name;
        string target;
        if (action.Saved is { } id && single == null && action.Stamp == null && owner is { } account
            && GetDrawings(account)?.Find(d => d.Id == id) is { } saved)
        {
            symbol = saved.Symbol;
            name = saved.Name;
            target = $"saved drawing {id} of {account}";
            if (action.Delete && Delete(account, id) is { } error)
                return error;
        }
        else if (action.Stamp is { } net && action.Saved == null && TryGetEntity(net, out var uid)
            && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid.Value) && TryComp<CrayonStampComponent>(uid, out var stamp)
            && (single != null ? uid == single : owner != null && stamp.Artist == owner))
        {
            symbol = stamp.Symbol;
            name = stamp.DrawingName ?? Name(uid.Value);
            target = $"floor drawing {ToPrettyString(uid.Value)} of {stamp.Artist}";
            if (action.Delete)
                QueueDel(uid.Value);
        }
        else
            return "pirate-crayon-library-changed";

        if (!action.Delete)
        {
            if (GetDrawings(admin.UserId) is not { } library)
                return "pirate-crayon-library-unavailable";

            var copy = name;
            for (var i = 2; library.Any(d => d.Name == copy); i++)
            {
                var suffix = $" ({i})";
                copy = name[..Math.Min(name.Length, CrayonDrawing.MaxNameLength - suffix.Length)] + suffix;
            }

            if (Save(admin.UserId, null, copy, symbol, out _) is { } error)
                return error;
        }

        _log.Add(LogType.CrayonDraw, LogImpact.Medium, $"{admin:actor} {(action.Delete ? "deleted" : "copied")} {target}");
        return action.Delete ? "pirate-crayon-admin-deleted" : "pirate-crayon-admin-copied";
    }
}
