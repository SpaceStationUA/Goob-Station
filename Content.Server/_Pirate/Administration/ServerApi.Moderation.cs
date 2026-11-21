// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server._Pirate.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Server.ServerStatus;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    [Dependency] private readonly IServerDbManager _moderationDb = default!;
    [Dependency] private readonly IBanManager _moderationBans = default!;
    [Dependency] private readonly IPlayerLocator _moderationPlayers = default!;
    [Dependency] private readonly IResourceManager _moderationResources = default!;
    [Dependency] private readonly IAdminLogManager _moderationLog = default!;

    private sealed record ModerationRequest(Guid RequestId, string Action, Guid Target, [property: JsonRequired] int Minutes, string Reason, string[] Roles);
    private sealed record ModerationResult(string State, int? BanId = null);

    private async Task ModeratePiratePlayer(IStatusHandlerContext context, Actor actor)
    {
        var request = await ReadJson<ModerationRequest>(context);
        if (request == null)
            return;

        if (request.RequestId == Guid.Empty || actor.Guid == Guid.Empty || request.Target == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1200
            || request.Roles == null || request.Minutes < 0
            || request.Action is not ("ban" or "roleban" or "pacify")
            || (request.Action == "pacify" && (request.Minutes == 0 || request.Minutes % 1440 != 0))
            || (request.Action == "roleban" ? request.Roles.Length is < 1 or > 512 : request.Roles.Length != 0))
        {
            await context.RespondJsonAsync(new ModerationResult("invalid"), HttpStatusCode.BadRequest);
            return;
        }

        await RunOnMainThread(async () =>
        {
            var actorId = new NetUserId(actor.Guid);
            var permission = request.Action == "pacify" ? AdminFlags.Admin : AdminFlags.Ban;
            if (!await CanModerateOffline(actorId, permission))
            {
                await context.RespondJsonAsync(new ModerationResult("forbidden"), HttpStatusCode.Forbidden);
                return;
            }

            var userData = _moderationResources.UserData;
            var directory = new ResPath("/pirate-moderation");
            userData.CreateDir(directory);
            var stem = directory / request.RequestId.ToString("N");
            var evidence = stem.WithName(stem.Filename + ".request");
            var resultPath = stem.WithName(stem.Filename + ".result");
            var received = JsonSerializer.Serialize(new { Actor = actor.Guid, Request = request });
            var fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(received)));
            if (userData.Exists(evidence))
            {
                await RespondToPriorModeration(context, userData, evidence, resultPath, fingerprint);
                return;
            }

            var target = await _moderationPlayers.LookupIdAsync(new NetUserId(request.Target));
            if (target == null)
            {
                await context.RespondJsonAsync(new ModerationResult("unknown_player"), HttpStatusCode.UnprocessableContent);
                return;
            }

            var roleBan = new CreateRoleBanInfo(request.Reason);
            foreach (var role in request.Roles)
            {
                if (role is { Length: > 4 and <= 68 } && role.StartsWith("Job:", StringComparison.Ordinal)
                    && _prototypeManager.HasIndex<JobPrototype>(role[4..]))
                    roleBan.AddJob(new ProtoId<JobPrototype>(role[4..]));
                else if (role is { Length: > 6 and <= 70 } && role.StartsWith("Antag:", StringComparison.Ordinal)
                    && _prototypeManager.HasIndex<AntagPrototype>(role[6..]))
                    roleBan.AddAntag(new ProtoId<AntagPrototype>(role[6..]));
                else if (role is { Length: > 11 and <= 75 } && role.StartsWith("Department:", StringComparison.Ordinal)
                    && _prototypeManager.TryIndex<DepartmentPrototype>(role[11..], out var department) && department.Roles.Count > 0)
                {
                    foreach (var job in department.Roles)
                        roleBan.AddJob(job);
                }
                else
                {
                    await context.RespondJsonAsync(new ModerationResult("unknown_role"), HttpStatusCode.UnprocessableContent);
                    return;
                }
            }

            // A request interrupted after the native mutation must not replay on round or process restart.
            // Keep the original actor and evidence even if the final acknowledgement is lost.
            try
            {
                using var stream = userData.Open(evidence, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.WriteLine(fingerprint);
                writer.Write(received);
                writer.Flush();
                if (stream is FileStream file)
                    file.Flush(true);
            }
            catch (IOException) when (userData.Exists(evidence))
            {
                await RespondToPriorModeration(context, userData, evidence, resultPath, fingerprint);
                return;
            }

            int? banId = null;
            if (request.Action == "pacify")
            {
                var pacify = new PirateModerationPacifyEvent(target.UserId, target.Username, request.Minutes / 1440);
                _entityManager.EventBus.RaiseEvent(EventSource.Local, pacify);
                if (!pacify.Handled)
                {
                    await context.RespondJsonAsync(new ModerationResult("pacification_unavailable"), HttpStatusCode.ServiceUnavailable);
                    return;
                }
                _moderationLog.Add(LogType.AdminCommands, LogImpact.High,
                    $"Discord administrator {actorId} pacified {target.Username} ({target.UserId}) for {pacify.Days} days. Reason: {request.Reason}");
            }
            else
            {
                var serverBan = new CreateServerBanInfo(request.Reason);
                CreateBanInfo ban = request.Action == "ban" ? serverBan : roleBan;
                ban.AddUser(target.UserId, target.Username);
                ban.AddHWId(target.LastHWId);
                ban.WithBanningAdmin(actorId);
                if (request.Minutes > 0)
                    ban.WithMinutes(request.Minutes);
                var defaultSeverity = request.Action == "ban" ? CCVars.ServerBanDefaultSeverity : CCVars.RoleBanDefaultSeverity;
                ban.WithSeverity(Enum.TryParse<NoteSeverity>(_config.GetCVar(defaultSeverity), out var severity)
                    ? severity : request.Action == "ban" ? NoteSeverity.High : NoteSeverity.Medium);
                banId = request.Action == "ban"
                    ? await _moderationBans.CreateServerBanAsync(serverBan)
                    : await _moderationBans.CreateRoleBanAsync(roleBan);
            }

            _sawmill.Info($"Discord moderation {request.RequestId}: {request.Action} actor={actor.Guid} target={target.UserId} ban={banId}");
            var response = new ModerationResult("applied", banId);
            var temporary = stem.WithName(stem.Filename + ".tmp");
            using (var stream = userData.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, response);
                if (stream is FileStream file)
                    file.Flush(true);
            }
            userData.Rename(temporary, resultPath);
            await context.RespondJsonAsync(response);
        });
    }

    private static async Task RespondToPriorModeration(IStatusHandlerContext context, IWritableDirProvider data,
        ResPath evidence, ResPath resultPath, string fingerprint)
    {
        using var reader = new StreamReader(data.Open(evidence, FileMode.Open, FileAccess.Read, FileShare.Read));
        var original = reader.ReadLine();
        if (original != null && original != fingerprint)
        {
            await context.RespondJsonAsync(new ModerationResult("different_request"), HttpStatusCode.Conflict);
            return;
        }
        if (original == fingerprint && data.Exists(resultPath))
        {
            using var file = data.Open(resultPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (JsonSerializer.Deserialize<ModerationResult>(file) is { State: "applied" } result)
            {
                await context.RespondJsonAsync(result);
                return;
            }
        }

        await context.RespondJsonAsync(new ModerationResult("pending"), HttpStatusCode.Accepted);
    }

    // Discord acts with permanent game permissions, including individual additions and removals.
    private async Task<bool> CanModerateOffline(NetUserId actor, AdminFlags permission)
    {
        var admin = await _moderationDb.GetAdminDataForAsync(actor);
        if (admin == null || admin.Deadminned || admin.Suspended)
            return false;

        var flags = admin.AdminRank == null ? AdminFlags.None : AdminFlagsHelper.NamesToFlags(admin.AdminRank.Flags.Select(f => f.Flag));
        foreach (var entry in admin.Flags)
        {
            var flag = AdminFlagsHelper.NameToFlag(entry.Flag);
            flags = entry.Negative ? flags & ~flag : flags | flag;
        }

        return (flags & permission) == permission;
    }
}
