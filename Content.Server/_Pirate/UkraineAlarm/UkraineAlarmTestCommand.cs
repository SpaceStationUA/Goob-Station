using Content.Server.Administration;
using Content.Shared._Pirate.UkraineAlarm;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Pirate.UkraineAlarm;

[AdminCommand(AdminFlags.Host)]
public sealed class UkraineAlarmTestCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => UkraineAlarmCommands.TestBilaTserkva;
    public override string Description => "Додає тестову тривогу Білоцерківського району до наступної відповіді /alerts.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entityManager.System<UkraineAlarmSystem>();
        if (!system.QueueBilaTserkvaTestAlarm())
        {
            shell.WriteError("Білоцерківський район ще не завантажено з UkraineAlarm API.");
            return;
        }

        shell.WriteLine("Тестову тривогу Білоцерківського району буде додано під час наступного запиту /alerts.");
    }
}
