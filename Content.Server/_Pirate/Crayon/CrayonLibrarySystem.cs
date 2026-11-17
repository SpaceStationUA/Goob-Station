// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Newbies;
using Content.Server.Administration.Logs;
using Content.Shared._Pirate.Crayon;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Crayon;

public sealed partial class CrayonLibrarySystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminLogManager _log = default!;
    [Dependency] private readonly CrayonStencilSystem _stencils = default!;
    [Dependency] private readonly NoviceCrayonProtectionSystem _protection = default!;

    private CrayonLibraryStore _store = default!;
    private readonly Dictionary<NetUserId, List<CrayonDrawing>> _libraries = new();
    private readonly Dictionary<NetUserId, TimeSpan> _budgets = new();

    public override void Initialize()
    {
        _store = new CrayonLibraryStore(_resources.UserData);
        SubscribeNetworkEvent<CrayonLibraryRequest>(OnRequest);
        SubscribeNetworkEvent<CrayonSaveDrawing>(OnSave);
        SubscribeNetworkEvent<CrayonDeleteDrawing>(OnDelete);
        _players.PlayerStatusChanged += OnStatusChanged;
        InitializeModeration();
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnStatusChanged;
        base.Shutdown();
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        _libraries.Remove(args.Session.UserId);
        _budgets.Remove(args.Session.UserId);
    }

    private bool SpendRequest(NetUserId owner)
    {
        var now = _timing.RealTime;
        var next = _budgets.GetValueOrDefault(owner);
        if (next < now)
            next = now;
        if (next - now >= TimeSpan.FromSeconds(4))
            return false;

        _budgets[owner] = next + TimeSpan.FromMilliseconds(250);
        return true;
    }

    public List<CrayonDrawing>? GetDrawings(NetUserId owner)
    {
        if (_libraries.TryGetValue(owner, out var cached))
            return cached;

        try
        {
            var drawings = _store.Read(owner);
            if (_players.TryGetSessionById(owner, out _))
                _libraries[owner] = drawings;
            return drawings;
        }
        catch (Exception e)
        {
            Log.Error($"Reading crayon drawings for {owner} failed: {e}");
            return null;
        }
    }

    private void OnRequest(CrayonLibraryRequest msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!SpendRequest(session.UserId))
            return;

        var drawings = GetDrawings(session.UserId);
        SendLibraryState(session, drawings, drawings == null ? "pirate-crayon-library-unavailable" : null);
    }

    private void OnSave(CrayonSaveDrawing msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!SpendRequest(session.UserId))
        {
            RaiseNetworkEvent(new CrayonSaveResult(msg.Request, null, "pirate-crayon-library-busy"), session);
            return;
        }

        var remaining = _protection.GetRemainingPlaytime(session);
        if (remaining != TimeSpan.Zero)
        {
            RaiseNetworkEvent(new CrayonLibraryState(null, remaining), session);
            RaiseNetworkEvent(new CrayonSaveResult(msg.Request, null, "novice-protection-crayon-stickers"), session);
            return;
        }

        var error = Save(session.UserId, msg.Previous, msg.Name, msg.Symbol, out var saved);
        RaiseNetworkEvent(new CrayonSaveResult(msg.Request, saved, error), session);
    }

    public string? Save(NetUserId owner, Guid? previous, string name, CrayonSymbol symbol, out Guid? saved)
    {
        saved = null;
        if (symbol?.IsValid() != true || string.IsNullOrWhiteSpace(name) || name.Length > CrayonDrawing.MaxNameLength
            || name.Any(char.IsControl))
            return "pirate-crayon-library-invalid";

        name = name.Trim();
        if (GetDrawings(owner) is not { } current)
            return "pirate-crayon-library-unavailable";

        var index = current.FindIndex(d => d.Id == previous);
        if (previous != null && index < 0)
            return "pirate-crayon-library-changed";
        if (current.Any(d => d.Id != previous && d.Name == name))
            return "pirate-crayon-library-duplicate";
        if (index < 0 && current.Count >= CrayonDrawing.Limit)
            return "pirate-crayon-library-full";

        var drawing = new CrayonDrawing(Guid.NewGuid(), name, symbol);
        var next = new List<CrayonDrawing>(current);
        if (index < 0)
            next.Add(drawing);
        else
            next[index] = drawing;

        if (!Write(owner, next))
            return "pirate-crayon-library-unavailable";

        if (previous != null)
            ClearSelected(previous.Value);
        saved = drawing.Id;
        return null;
    }

    public string? Delete(NetUserId owner, Guid id)
    {
        if (GetDrawings(owner) is not { } current)
            return "pirate-crayon-library-unavailable";

        var next = new List<CrayonDrawing>(current);
        if (next.RemoveAll(d => d.Id == id) == 0)
            return "pirate-crayon-library-changed";
        if (!Write(owner, next))
            return "pirate-crayon-library-unavailable";

        ClearSelected(id);
        return null;
    }

    private void ClearSelected(Guid id)
    {
        var query = EntityQueryEnumerator<CrayonStencilComponent>();
        while (query.MoveNext(out var uid, out var selection))
        {
            if (selection.DrawingId == id)
                _stencils.ClearStencil(uid);
        }
    }

    private void OnDelete(CrayonDeleteDrawing msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var error = SpendRequest(session.UserId) ? Delete(session.UserId, msg.Id) : "pirate-crayon-library-busy";
        if (error != null)
            SendLibraryState(session, null, error);
    }

    private void SendLibraryState(ICommonSession session, List<CrayonDrawing>? drawings, string? error = null)
    {
        RaiseNetworkEvent(new CrayonLibraryState(drawings, _protection.GetRemainingPlaytime(session), error), session);
    }

    private bool Write(NetUserId owner, List<CrayonDrawing> next)
    {
        try
        {
            _store.Write(owner, next);
        }
        catch (Exception e)
        {
            _libraries.Remove(owner);
            Log.Error($"Writing crayon drawings for {owner} failed: {e}");
            return false;
        }

        if (_players.TryGetSessionById(owner, out var session))
        {
            _libraries[owner] = next;
            SendLibraryState(session, next);
        }

        return true;
    }
}
