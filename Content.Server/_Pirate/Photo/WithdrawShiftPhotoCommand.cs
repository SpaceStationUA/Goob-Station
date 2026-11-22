// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Pirate.Photo;

[AnyCommand]
public sealed class WithdrawShiftPhotoCommand : LocalizedEntityCommands
{
    public override string Command => "withdrawshiftphoto";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var removed = shell.Player is {} session && EntityManager.System<PhotoSystem>().WithdrawDiscordPhoto(session.UserId);
        shell.WriteLine(Loc.GetString(removed ? "pirate-shift-photo-withdrawn" : "pirate-shift-photo-none"));
    }
}
