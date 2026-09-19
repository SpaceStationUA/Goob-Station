// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using System.Linq;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class ChatBanCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _bans = default!;

    public override string Command => "chatban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 5)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryParseChatBanType(args[1], out var type))
        {
            shell.WriteError(Loc.GetString("chat-ban-invalid-channel", ("channel", args[1])));
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-ban-player"));
            return;
        }

        uint minutes = 0;
        if (args.Length >= 4 && !uint.TryParse(args[3], out minutes))
        {
            shell.WriteError(Loc.GetString("chat-ban-invalid-duration", ("duration", args[3])));
            return;
        }

        NoteSeverity? severity = null;
        if (args.Length == 5)
        {
            if (!Enum.TryParse(args[4], true, out NoteSeverity parsedSeverity))
            {
                shell.WriteError(Loc.GetString("chat-ban-invalid-severity", ("severity", args[4])));
                return;
            }

            severity = parsedSeverity;
        }

        var info = new CreateChatBanInfo(type, args[2]);
        info.AddUser(located.UserId, located.Username);
        info.WithBanningAdmin(shell.Player?.UserId);
        if (minutes > 0)
            info.WithMinutes(minutes);
        if (severity != null)
            info.WithSeverity(severity.Value);

        await _bans.CreateChatBan(info);
        shell.WriteLine(Loc.GetString("chat-ban-command-success",
            ("player", located.Username),
            ("channel", BanManager.GetChatBanChannelName(type))));
    }

    internal static bool TryParseChatBanType(string value, out BanType type)
    {
        type = value.ToLowerInvariant() switch
        {
            "ooc" => BanType.OOC,
            "looc" => BanType.LOOC,
            "dead" or "deadchat" => BanType.Deadchat,
            _ => default,
        };
        return BanManager.IsChatBanType(type);
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed class ChatUnbanCommand : LocalizedCommands
{
    [Dependency] private readonly IBanManager _bans = default!;

    public override string Command => "chatunban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var banId))
        {
            shell.WriteLine(Help);
            return;
        }

        shell.WriteLine(await _bans.PardonChatBan(banId, shell.Player?.UserId, DateTimeOffset.Now));
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed class ChatBanListCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _bans = default!;

    public override string Command => "chatbanlist";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-ban-player"));
            return;
        }

        var bans = await _bans.GetChatBans(located.UserId);
        if (bans.Count == 0)
        {
            shell.WriteLine(Loc.GetString("chat-ban-list-empty", ("player", located.Username)));
            return;
        }

        foreach (var ban in bans.OrderByDescending(ban => ban.BanTime))
        {
            var status = ban.Unban != null
                ? Loc.GetString("chat-ban-list-pardoned")
                : ban.ExpirationTime is { } expiry && expiry <= DateTimeOffset.Now
                    ? Loc.GetString("chat-ban-list-expired")
                    : Loc.GetString("chat-ban-list-active");
            var expiryText = ban.ExpirationTime == null
                ? Loc.GetString("chat-ban-permanent")
                : ban.ExpirationTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            shell.WriteLine(Loc.GetString("chat-ban-list-line",
                ("id", ban.Id ?? 0),
                ("channel", BanManager.GetChatBanChannelName(ban.Type)),
                ("status", status),
                ("expiry", expiryText),
                ("reason", ban.Reason)));
        }
    }
}
