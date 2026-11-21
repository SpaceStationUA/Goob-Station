// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Pirate.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Robust.Server.ServerStatus;
using Robust.Shared.Network;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    private readonly SemaphoreSlim _pardonGate = new(1);

    private sealed record RestrictionLookup(Guid Target);
    private sealed record Restriction(string Kind, int? BanId, long? Expires, string? Reason, string[] Roles,
        int AffectedAccounts, string Version);
    private sealed record PardonRequest(Guid RequestId, Guid Target, string Kind, int? BanId, string Version, string Reason);

    private async Task ReviewPacifications(IStatusHandlerContext context, Actor actor)
    {
        await RunOnMainThread(async () =>
        {
            if (actor.Guid == Guid.Empty || !await CanModerateOffline(new NetUserId(actor.Guid), AdminFlags.Moderator))
            {
                await context.RespondJsonAsync(new { State = "forbidden" }, HttpStatusCode.Forbidden);
                return;
            }

            var list = new PiratePacificationReviewEvent();
            _entityManager.EventBus.RaiseEvent(EventSource.Local, list);
            if (list.Active == null)
            {
                await context.RespondJsonAsync(new { State = "unavailable" }, HttpStatusCode.ServiceUnavailable);
                return;
            }
            await context.RespondJsonAsync(new { Active = list.Active });
        });
    }

    private async Task ReviewRestrictions(IStatusHandlerContext context, Actor actor)
    {
        var request = await ReadJson<RestrictionLookup>(context);
        if (request == null)
            return;
        if (request.Target == Guid.Empty || actor.Guid == Guid.Empty)
        {
            await context.RespondJsonAsync(new { State = "invalid" }, HttpStatusCode.BadRequest);
            return;
        }
        await RunOnMainThread(async () =>
        {
            var admin = new NetUserId(actor.Guid);
            var ban = await CanModerateOffline(admin, AdminFlags.Ban);
            var pacify = await CanModerateOffline(admin, AdminFlags.Moderator);
            if (!ban && !pacify)
            {
                await context.RespondJsonAsync(new { State = "forbidden" }, HttpStatusCode.Forbidden);
                return;
            }

            var target = new NetUserId(request.Target);
            var active = new List<Restriction>();
            if (ban)
            {
                foreach (var type in new[] { BanType.Server, BanType.Role })
                {
                    var matches = await _moderationDb.GetBansAsync(null, target, null, null, false, type);
                    foreach (var entry in matches.Where(b => b.Id != null && b.UserIds.Contains(target)).OrderByDescending(b => b.Id))
                        active.Add(DescribeRestriction(entry));
                }
            }

            if (pacify)
            {
                var list = new PiratePacificationReviewEvent();
                _entityManager.EventBus.RaiseEvent(EventSource.Local, list);
                if (list.Active == null)
                {
                    await context.RespondJsonAsync(new { State = "unavailable" }, HttpStatusCode.ServiceUnavailable);
                    return;
                }
                if (list.Active.FirstOrDefault(p => p.Target == request.Target) is { } manual)
                    active.Add(new Restriction("pacify", null, manual.Expires, null, [], 1, manual.Version));
            }
            await context.RespondJsonAsync(new { Active = active });
        });
    }

    private static Restriction DescribeRestriction(BanDef ban)
    {
        var roles = ban.Roles?.Select(r => r.ToString()).Order(StringComparer.Ordinal).ToArray() ?? [];
        var subjects = ban.UserIds.Select(u => u.UserId).Order().ToArray();
        // Selector values stay inside the game. Only their combined digest leaves this process.
        var networks = ban.Addresses.Select(a => $"{a.address}/{a.cidrMask}").Order(StringComparer.Ordinal);
        var hardware = ban.HWIds.Select(h => h.ToString()).Order(StringComparer.Ordinal);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { ban.Id, ban.Type, Expires = ban.ExpirationTime?.UtcTicks,
            ban.Reason, ban.Severity, ban.ExemptFlags, Roles = roles, Subjects = subjects, Networks = networks, Hardware = hardware });
        var version = Convert.ToHexString(SHA256.HashData(bytes));
        return new Restriction(ban.Type == BanType.Role ? "role" : "server", ban.Id,
            ban.ExpirationTime?.ToUnixTimeSeconds(), ban.Reason, roles, subjects.Length, version);
    }

    private async Task PardonRestriction(IStatusHandlerContext context, Actor actor)
    {
        var request = await ReadJson<PardonRequest>(context);
        if (request == null)
            return;
        if (actor.Guid == Guid.Empty || request.RequestId == Guid.Empty || request.Target == Guid.Empty ||
            request.Kind is not ("server" or "role" or "pacify") || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 1200 || string.IsNullOrWhiteSpace(request.Version) || request.Version.Length > 64 ||
            (request.Kind == "pacify" ? request.BanId != null : request.BanId is null or <= 0))
        {
            await context.RespondJsonAsync(new { State = "invalid" }, HttpStatusCode.BadRequest);
            return;
        }

        await _pardonGate.WaitAsync();
        try
        {
            await RunOnMainThread(async () =>
            {
                var admin = new NetUserId(actor.Guid);
                var flag = request.Kind == "pacify" ? AdminFlags.Moderator : AdminFlags.Ban;
                if (!await CanModerateOffline(admin, flag))
                {
                    await context.RespondJsonAsync(new { State = "forbidden" }, HttpStatusCode.Forbidden);
                    return;
                }

                var disk = _moderationResources.UserData;
                var dir = new ResPath("/pirate-pardons");
                disk.CreateDir(dir);
                var stem = request.RequestId.ToString("N");
                var intent = dir / (stem + ".request");
                var result = dir / (stem + ".result");
                var original = JsonSerializer.Serialize(new { Actor = actor.Guid, Request = request });
                if (disk.Exists(intent) && disk.ReadAllText(intent) != original)
                {
                    await context.RespondJsonAsync(new { State = "conflict" }, HttpStatusCode.Conflict);
                    return;
                }

                string state;
                if (disk.Exists(result))
                {
                    state = disk.ReadAllText(result);
                }
                else
                {
                    // Recording the reason before touching the native record preserves the audit across a process restart.
                    if (!disk.Exists(intent))
                        WritePardonRecord(disk, intent, original);
                    state = await ApplyPardon(request, admin);
                    if (state == "applied")
                    {
                        var reason = request.Reason.Trim().ReplaceLineEndings(" ");
                        _moderationLog.Add(LogType.AdminCommands, LogImpact.High,
                            $"Discord administrator {actor.Guid} pardoned {request.Kind} {request.BanId} for {request.Target}. Request {request.RequestId}, reason: {reason}");
                        _sawmill.Info($"Discord pardon {request.RequestId}: actor={actor.Guid}, {request.Kind}={request.BanId} target={request.Target} reason={reason}");
                    }
                    if (state != "unavailable")
                        WritePardonRecord(disk, result, state);
                }

                var status = state switch
                {
                    "applied" or "inactive" => HttpStatusCode.OK,
                    "changed" => HttpStatusCode.Conflict,
                    "unavailable" => HttpStatusCode.ServiceUnavailable,
                    _ => HttpStatusCode.NotFound,
                };
                await context.RespondJsonAsync(new { State = state, request.BanId }, status);
            });
        }
        finally
        {
            _pardonGate.Release();
        }
    }

    private static void WritePardonRecord(IWritableDirProvider disk, ResPath path, string value)
    {
        // The gate serializes writers; an interrupted temporary file is safe to overwrite on retry.
        var temporary = path.WithName(path.Filename + ".tmp");
        using (var stream = disk.Open(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(value);
            writer.Flush();
            if (stream is FileStream file)
                file.Flush(true);
        }
        disk.Rename(temporary, path);
    }

    private async Task<string> ApplyPardon(PardonRequest request, NetUserId admin)
    {
        if (request.Kind == "pacify")
        {
            var pardon = new PiratePardonPacificationEvent(request.Target, request.Version);
            _entityManager.EventBus.RaiseEvent(EventSource.Local, pardon);
            return pardon.State ?? "unavailable";
        }

        var id = request.BanId!.Value;
        var type = request.Kind == "role" ? BanType.Role : BanType.Server;
        var ban = await _moderationDb.GetBanAsync(id);
        if (ban == null || ban.Type != type || !ban.UserIds.Contains(new NetUserId(request.Target)))
            return "missing";
        if (ban.Unban != null || ban.ExpirationTime <= DateTimeOffset.UtcNow)
            return "inactive";
        if (DescribeRestriction(ban).Version != request.Version)
            return "changed";

        try
        {
            if (type == BanType.Role)
            {
                await _moderationBans.PardonRoleBan(id, admin, DateTimeOffset.UtcNow);
                // The in-game role manager also returns normally if another moderator already pardoned it.
                var result = await _moderationDb.GetBanAsync(id);
                if (result?.Unban?.UnbanningAdmin != admin)
                    return "inactive";
            }
            else
                await _moderationDb.AddUnbanAsync(new UnbanDef(id, admin, DateTimeOffset.UtcNow));
        }
        catch (DbUpdateException)
        {
            // An in-game moderator can pardon the same decision while our database write awaits.
            if ((await _moderationDb.GetBanAsync(id))?.Unban != null)
                return "inactive";
            throw;
        }
        return "applied";
    }
}
