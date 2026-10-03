// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Pirate.Chat.Commands;

// Either flag grants command access; the channel permission is checked again by the chat manager.
internal abstract class StaffChatCommand : LocalizedCommands
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    protected abstract OOCChatType ChatType { get; }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        _chatManager.TrySendOOCMessage(player, message, ChatType);
    }
}

[AdminCommand(AdminFlags.MentorChat)]
[AdminCommand(AdminFlags.Adminchat)]
internal sealed class MentorChatCommand : StaffChatCommand
{
    public override string Command => "msay";
    protected override OOCChatType ChatType => OOCChatType.MentorChat;
}

[AdminCommand(AdminFlags.EventChat)]
[AdminCommand(AdminFlags.Adminchat)]
internal sealed class EventChatCommand : StaffChatCommand
{
    public override string Command => "esay";
    protected override OOCChatType ChatType => OOCChatType.EventChat;
}

[AdminCommand(AdminFlags.CentComChat)]
[AdminCommand(AdminFlags.Adminchat)]
internal sealed class CentComChatCommand : StaffChatCommand
{
    public override string Command => "ccsay";
    protected override OOCChatType ChatType => OOCChatType.CentComChat;
}
