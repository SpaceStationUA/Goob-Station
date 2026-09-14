using Content.Client.Administration.Managers;
using Content.Shared._Pirate.UkraineAlarm;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Pirate.UkraineAlarm;

public sealed class UkraineAlarmTestCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IClientAdminManager _adminManager = default!;

    public override string Command => UkraineAlarmCommands.TestBilaTserkva;
    public override string Description => "Додає тестову тривогу Білоцерківського району до наступної відповіді /alerts.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_adminManager.HasFlag(AdminFlags.Host))
        {
            shell.WriteError(Loc.GetString("shell-missing-required-permission", ("perm", "+HOST")));
            return;
        }

        shell.RemoteExecuteCommand(Command);
    }
}
