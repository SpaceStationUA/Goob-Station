using Content.Server.Administration;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared.Administration;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Psionics;

[AdminCommand(AdminFlags.Admin)] // Pirate: match the rest of the psionic command set (makepsionic, psionicadd, etc.)
public sealed class ListPsionicsCommand : IConsoleCommand
{
    public string Command => "lspsionics";
    public string Description => Loc.GetString("command-lspsionic-description");
    public string Help => Loc.GetString("command-lspsionic-help");
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        foreach (var (actor, mob, psionic, meta) in entMan.EntityQuery<ActorComponent, MobStateComponent, PsionicComponent, MetaDataComponent>())
        {
            // filter out xenos, etc, with innate telepathy
            if (psionic.PsionicPowersActionEntities.Count == 0)
                continue;

            var psiPowerNames = new List<string>();
            foreach (var comp in entMan.GetComponents(meta.Owner))
            {
                if (comp is BasePsionicPowerComponent power)
                    psiPowerNames.Add(Loc.GetString(power.PowerName));
            }

            shell.WriteLine($"{meta.EntityName} ({meta.Owner}) - {actor.PlayerSession.Name}: {string.Join(", ", psiPowerNames)}");
        }
    }
}
