// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.TV;
using Content.Server.Administration.Managers;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Pirate.Server.TV;

/// <summary>
///     The Pirate TV: per-television channel/queue/clock/lock.
///     A standalone TV owns its state; TVs linked with the multitool or
///     network configurator (DeviceLink) form a star — one master whose
///     broadcast mirrors feed into any number of sink TVs. Mirrors forward
///     every control back to the master, so the group stays coherent, and
///     unlinking resets the mirror to off.
///     When a TV is locked, only transport controls (play/pause/seek/
///     next-on-end) pass; every picker/jumper/lock-toggle is rejected.
/// </summary>
public sealed class PirateTvSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _link = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string SourcePort = "PirateTvBroadcast";
    private const string SinkPort = "PirateTvReceive";

    /// <summary>Collapses duplicate "ended" pings from several mirrored viewers.</summary>
    private readonly Dictionary<EntityUid, long> _lastEndedMs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PirateTvComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<PirateTvComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<PirateTvComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<PirateTvComponent, ComponentShutdown>(OnShutdown);

        SubscribeNetworkEvent<PirateTvRequestEvent>(OnRequest);
        SubscribeNetworkEvent<PirateTvPickEvent>(OnPick);
        SubscribeNetworkEvent<PirateTvCommandEvent>(OnCommand);
        SubscribeNetworkEvent<PirateTvQueueAddEvent>(OnQueueAdd);
        SubscribeNetworkEvent<PirateTvQueueNavEvent>(OnQueueNav);
        SubscribeNetworkEvent<PirateTvQueueRemoveEvent>(OnQueueRemove);
        SubscribeNetworkEvent<PirateTvQueueMoveEvent>(OnQueueMove);
        SubscribeNetworkEvent<PirateTvLockEvent>(OnLockToggle);
    }

    // ===== DeviceLink: star groups =====

    private void OnLinkAttempt(Entity<PirateTvComponent> ent, ref LinkAttemptEvent args)
    {
        // Chains are allowed (1→2→3): a source may itself be a mirror, and a
        // sink may have its own mirrors. The only illegal shape is a cycle.
        if (args.Sink == ent.Owner)
        {
            if (args.SinkPort != SinkPort)
                return;
            if (args.SourcePort != SourcePort ||
                args.Source == ent.Owner ||
                !TryComp<PirateTvComponent>(args.Source, out _) ||
                WouldCycle(ent.Owner, args.Source))
            {
                args.Cancel();
            }
            return;
        }

        if (args.Source == ent.Owner)
        {
            if (args.SourcePort != SourcePort)
                return;
            if (!TryComp<PirateTvComponent>(args.Sink, out _) ||
                args.SinkPort != SinkPort ||
                WouldCycle(args.Sink, ent.Owner))
            {
                args.Cancel();
            }
        }
    }

    /// <summary>
    ///     True when linking <paramref name="sink"/> under
    ///     <paramref name="source"/> would create a cycle (the sink is
    ///     already an ancestor of the source).
    /// </summary>
    private bool WouldCycle(EntityUid sink, EntityUid source)
    {
        // Walk source's chain up to its root; if we pass through `sink`, the
        // new link would close the loop.
        var cursor = source;
        for (var guard = 0; guard < 64; guard++)
        {
            if (cursor == sink)
                return true;
            if (!TryComp<PirateTvComponent>(cursor, out var comp) || !comp.Source.IsValid() ||
                !TryGetEntity(comp.Source, out var parent))
            {
                return false;
            }
            cursor = parent.Value;
        }
        return true;
    }

    private void OnNewLink(Entity<PirateTvComponent> ent, ref NewLinkEvent args)
    {
        if (args.Sink != ent.Owner || args.SinkPort != SinkPort)
            return;

        if (!TryComp<PirateTvComponent>(args.Source, out var sourceComp))
            return;

        // Re-linking: drop the old parent's link first, without letting its
        // disconnect event reset us (we re-copy the new parent's state below).
        if (ent.Comp.Source.IsValid() &&
            TryGetEntity(ent.Comp.Source, out var oldUid) &&
            oldUid.Value != args.Source &&
            TryComp<DeviceLinkSourceComponent>(oldUid, out var oldSrc))
        {
            _reparenting.Add(ent.Owner);
            try
            {
                _link.RemoveSinkFromSource(oldUid.Value, ent.Owner, oldSrc);
            }
            finally
            {
                _reparenting.Remove(ent.Owner);
            }
        }

        ent.Comp.Source = GetNetEntity(args.Source);
        CopyState(sourceComp, ent.Comp);
        Mutate(ent.Owner, ent.Comp);
    }

    private void OnPortDisconnected(Entity<PirateTvComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != SinkPort || !ent.Comp.Source.IsValid())
            return;
        if (_reparenting.Contains(ent.Owner))
            return;

        // Unlink: this TV becomes its own root (off/empty). Its own mirrors
        // stay attached to it, so a 1→2→3 chain unlinked at 1 becomes 2→3.
        ent.Comp.Source = NetEntity.Invalid;
        ResetState(ent.Comp);
        Mutate(ent.Owner, ent.Comp);
    }

    /// <summary>TVs currently being re-pointed; their disconnect must not reset them.</summary>
    private readonly HashSet<EntityUid> _reparenting = new();

    private void OnShutdown(Entity<PirateTvComponent> ent, ref ComponentShutdown args)
    {
        // A dying root frees its mirrors (they reset to off); a dying mirror
        // re-parents its own mirrors onto its parent, keeping the chain alive.
        var net = GetNetEntity(ent.Owner);
        var newParent = ent.Comp.Source;

        var q = EntityQueryEnumerator<PirateTvComponent>();
        while (q.MoveNext(out var uid, out var child))
        {
            if (child.Source != net)
                continue;
            child.Source = newParent;
            if (newParent.IsValid() && TryGetEntity(newParent, out var parentUid) &&
                TryComp(parentUid.Value, out PirateTvComponent? parentComp))
            {
                CopyState(parentComp, child);
            }
            else
            {
                ResetState(child);
            }
            PushState(uid, child);
        }
    }

    // ===== client → server =====

    private void OnRequest(PirateTvRequestEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out var comp))
            return;
        RaiseNetworkEvent(BuildState(uid, comp), args.SenderSession.Channel);
    }

    private void OnPick(PirateTvPickEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        if (master.Locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — канал не міняється");
            return;
        }

        Pick(masterUid, master, msg.Url, msg.Kind, msg.Label, msg.Title);
        Feed(args, "поставив на ТБ: " + msg.Label);
    }

    /// <summary>
    ///     Puts a URL on the TV (reusing an existing queue entry if present).
    ///     Shared by the network handler and tests; no permission checks.
    /// </summary>
    public void Pick(EntityUid masterUid, PirateTvComponent master, string url, int kind, string label, string title)
    {
        if (url.Length == 0)
            return;

        foreach (var item in master.Queue)
        {
            if (item.Url != url)
                continue;
            NavTo(masterUid, master, master.Queue.IndexOf(item));
            return;
        }

        master.Queue.Add(new PirateTvQueueItem { Url = url, Kind = kind, Label = label, Title = title });
        NavTo(masterUid, master, master.Queue.Count - 1);
    }

    /// <summary>Applies a seek to the room (public for tests/probes).</summary>
    public void ApplySeek(EntityUid uid, PirateTvComponent comp, double pos)
    {
        var (masterUid, master) = ResolveMaster(uid, comp);
        master.Pos = Math.Max(0, pos);
        master.Stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        master.Playing = true;
        Mutate(masterUid, master);
    }

    private void OnQueueAdd(PirateTvQueueAddEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        if (master.Locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }

        if (msg.Url.Length == 0)
            return;

        foreach (var item in master.Queue)
        {
            if (item.Url == msg.Url)
                return;
        }

        master.Queue.Add(new PirateTvQueueItem
        {
            Url = msg.Url, Kind = msg.Kind, Label = msg.Label, Title = msg.Title,
        });

        if (master.Url.Length == 0)
        {
            // Empty room: the add becomes "play now".
            NavTo(masterUid, master, master.Queue.Count - 1);
            return;
        }

        Mutate(masterUid, master);
        Feed(args, "додав у чергу: " + msg.Label);
    }

    private void OnQueueNav(PirateTvQueueNavEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        if (master.Locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — черга зафіксована");
            return;
        }
        if (msg.Index < 0 || msg.Index >= master.Queue.Count)
            return;

        NavTo(masterUid, master, msg.Index);
        Feed(args, "переключив на елемент " + (msg.Index + 1));
    }

    private void OnQueueRemove(PirateTvQueueRemoveEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        if (master.Locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }
        if (msg.Index < 0 || msg.Index >= master.Queue.Count)
            return;

        var it = master.Queue[msg.Index];
        var wasNow = msg.Index == master.Now;
        master.Queue.RemoveAt(msg.Index);

        if (master.Queue.Count == 0)
        {
            ResetState(master);
            Mutate(masterUid, master);
            Feed(args, "очистив чергу");
            return;
        }

        if (wasNow)
        {
            NavTo(masterUid, master, Math.Min(msg.Index, master.Queue.Count - 1), silent: true);
            Feed(args, "прибрав " + it.Label + " — грає наступний");
            return;
        }

        if (master.Now > msg.Index)
            master.Now--;
        Mutate(masterUid, master);
        Feed(args, "прибрав " + it.Label);
    }

    private void OnQueueMove(PirateTvQueueMoveEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        if (master.Locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }
        if (msg.Index < 0 || msg.Index >= master.Queue.Count)
            return;

        var j = msg.Index + Math.Sign(msg.Delta);
        if (j < 0 || j >= master.Queue.Count)
            return;

        (master.Queue[msg.Index], master.Queue[j]) = (master.Queue[j], master.Queue[msg.Index]);

        if (master.Now == msg.Index)
            master.Now = j;
        else if (master.Now == j)
            master.Now = msg.Index;

        Mutate(masterUid, master);
    }

    private void OnLockToggle(PirateTvLockEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        master.Locked = msg.Locked;
        Mutate(masterUid, master);
        Feed(args, master.Locked ? "заблокував ТБ" : "розблокував ТБ");
    }

    private void OnCommand(PirateTvCommandEvent msg, EntitySessionEventArgs args)
    {
        if (!TryResolveTv(msg.Tv, out var uid, out _) || !InReach(args, uid))
            return;

        var (masterUid, master) = ResolveMaster(uid, Comp<PirateTvComponent>(uid));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        switch (msg.Op)
        {
            case "pause":
                // Anchor to the LIVE room position, never the client's guess:
                // the room clock is a pure function of Playing, so pausing at
                // wherever the room actually is, then resuming, cannot rewind.
                if (!master.Playing)
                    break; // already paused — idempotent
                master.Pos = Math.Max(0, CurrentPos(master, now));
                master.Playing = false;
                master.Stamp = now;
                break;
            case "play":
                if (master.Playing)
                    break; // already playing — pressing play again must not re-anchor
                // Resume from the current (paused) position; never touch Pos.
                master.Playing = true;
                master.Stamp = now;
                break;
            case "seekTo":
                master.Pos = Math.Max(0, msg.Arg);
                master.Stamp = now;
                master.Playing = true; // seeking implies we mean to play
                break;
            case "ended":
                // Auto-advance, deduped: every mirrored page reports at once.
                if (_lastEndedMs.TryGetValue(masterUid, out var last) && now - last < 3000)
                    return;
                _lastEndedMs[masterUid] = now;
                NavTo(masterUid, master, master.Now + 1 < master.Queue.Count ? master.Now + 1 : 0,
                    silent: true);
                return;
            case "manual_next":
                if (master.Locked && !IsAdmin(args))
                {
                    Feed(args, "ТБ заблоковано — наступний відео недоступний");
                    return;
                }
                NavTo(masterUid, master, master.Now + 1 < master.Queue.Count ? master.Now + 1 : 0,
                    silent: true);
                Feed(args, "наступний відео");
                return;
            case "set_title":
                // The page reported its own video title; stamp the current
                // playlist entry so every queue row shows the real name.
                if (msg.Title.Length == 0 || master.Now < 0 || master.Now >= master.Queue.Count)
                    return;
                var title = msg.Title[..Math.Min(msg.Title.Length, 100)];
                if (master.Queue[master.Now].Title == title)
                    return;
                master.Queue[master.Now].Title = title;
                break;
            case "mute":
                master.Muted = msg.Arg > 0.5;
                break;
            default:
                return;
        }

        Mutate(masterUid, master);
        Feed(args, msg.Op switch
        {
            "pause" => "ставить на паузу",
            "play" => "вмикає",
            "seekTo" => "перематує",
            "mute" => master.Muted ? "вимикає звук" : "вмикає звук",
            _ => msg.Op,
        });
    }

    // ===== core =====

    private bool TryResolveTv(NetEntity net, out EntityUid uid, out PirateTvComponent comp)
    {
        comp = default!;
        if (!TryGetEntity(net, out var found) ||
            !TryComp(found.Value, out PirateTvComponent? tv))
        {
            uid = default;
            return false;
        }
        uid = found.Value;
        comp = tv;
        return true;
    }

    /// <summary>
    ///     A mirror forwards every control to the root of its chain (the TV
    ///     at the top that actually owns the state).
    /// </summary>
    private (EntityUid Uid, PirateTvComponent Comp) ResolveMaster(EntityUid uid, PirateTvComponent comp)
    {
        var cursorUid = uid;
        var cursor = comp;
        for (var guard = 0; guard < 64; guard++)
        {
            if (!cursor.Source.IsValid() ||
                !TryGetEntity(cursor.Source, out var parentUid) ||
                !TryComp(parentUid.Value, out PirateTvComponent? parent))
            {
                break;
            }
            cursorUid = parentUid.Value;
            cursor = parent;
        }
        return (cursorUid, cursor);
    }

    private bool InReach(EntitySessionEventArgs args, EntityUid tv)
    {
        var ent = args.SenderSession.AttachedEntity;
        if (ent == null)
            return false;
        var here = _transform.GetMapCoordinates(ent.Value);
        var there = _transform.GetMapCoordinates(tv);
        return here.MapId == there.MapId && here.InRange(there, 12f);
    }

    private bool IsAdmin(EntitySessionEventArgs args)
    {
        var ent = args.SenderSession.AttachedEntity;
        return ent != null && _admin.IsAdmin(ent.Value);
    }

    /// <summary>
    ///     The TV's position right now: Pos plus wall time since Stamp while
    ///     playing, else Pos. Mirrors the client's VideoPos so both agree.
    /// </summary>
    private static double CurrentPos(PirateTvComponent comp, long nowMs)
    {
        if (!comp.Playing)
            return comp.Pos;
        return comp.Pos + Math.Max(0, nowMs - comp.Stamp) / 1000.0;
    }

    /// <summary>Starts playing queue[index] (wraps to 0 past the end).</summary>
    private void NavTo(EntityUid uid, PirateTvComponent comp, int index, bool silent = false)
    {
        if (comp.Queue.Count == 0)
        {
            ResetState(comp);
            Mutate(uid, comp);
            return;
        }

        comp.Now = index < 0 ? comp.Queue.Count - 1 : index % comp.Queue.Count;
        var cur = comp.Queue[comp.Now];
        comp.Url = cur.Url;
        comp.Kind = cur.Kind;
        comp.Label = cur.Label;
        comp.Pos = 0;
        comp.Playing = true;
        comp.Stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Mutate(uid, comp);
        _ = silent;
    }

    /// <summary>
    ///     Pushes a root's state to itself and copies it down the whole
    ///     chain (1→2→3 means 3 also gets it).
    /// </summary>
    private void Mutate(EntityUid uid, PirateTvComponent comp)
    {
        PushState(uid, comp);

        var net = GetNetEntity(uid);
        CopyToChildren(net, comp, new HashSet<EntityUid> { uid });
    }

    private void CopyToChildren(NetEntity parent, PirateTvComponent source, HashSet<EntityUid> visited)
    {
        var q = EntityQueryEnumerator<PirateTvComponent>();
        while (q.MoveNext(out var childUid, out var child))
        {
            if (child.Source != parent || !visited.Add(childUid))
                continue;
            CopyState(source, child);
            PushState(childUid, child);
            CopyToChildren(GetNetEntity(childUid), child, visited);
        }
    }

    private static void CopyState(PirateTvComponent from, PirateTvComponent to)
    {
        to.Url = from.Url;
        to.Kind = from.Kind;
        to.Label = from.Label;
        to.Playing = from.Playing;
        to.Muted = from.Muted;
        to.Pos = from.Pos;
        to.Stamp = from.Stamp;
        to.Locked = from.Locked;
        to.Now = from.Now;
        to.Queue = new List<PirateTvQueueItem>(from.Queue);
    }

    private static void ResetState(PirateTvComponent comp)
    {
        comp.Url = "";
        comp.Kind = 0;
        comp.Label = "";
        comp.Playing = false;
        comp.Muted = false;
        comp.Pos = 0;
        comp.Stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        comp.Locked = false;
        comp.Now = -1;
        comp.Queue.Clear();
    }

    private PirateTvStateEvent BuildState(EntityUid uid, PirateTvComponent comp)
    {
        return new PirateTvStateEvent
        {
            Tv = GetNetEntity(uid),
            Source = comp.Source,
            Url = comp.Url,
            Kind = comp.Kind,
            Label = comp.Label,
            Playing = comp.Playing,
            Muted = comp.Muted,
            Pos = comp.Pos,
            Stamp = comp.Stamp,
            Locked = comp.Locked,
            Now = comp.Now,
            Items = new List<PirateTvQueueItem>(comp.Queue),
        };
    }

    private void PushState(EntityUid uid, PirateTvComponent comp)
        => RaiseNetworkEvent(BuildState(uid, comp), Filter.Pvs(uid, entityManager: EntityManager));

    /// <summary>World popup feed: "who did what" at the actor's feet.</summary>
    private void Feed(EntitySessionEventArgs args, string action)
    {
        var ent = args.SenderSession.AttachedEntity;
        if (ent == null)
            return;
        _popup.PopupEntity(action, ent.Value, Filter.Pvs(ent.Value), false, PopupType.Medium);
    }
}
