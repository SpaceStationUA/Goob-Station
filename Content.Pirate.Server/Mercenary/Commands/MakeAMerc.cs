using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Mercenary.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed class MakeAMerc : IConsoleCommand
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly IBanManager _banManager = default!;
        [Dependency] private readonly IEntityManager EntityManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        private static readonly List<ProtoId<AntagPrototype>> MercenaryBan = ["Mercenary"];

        // [Dependency] private readonly RevolutionaryRuleSystem _revolutionaryRule = default!;

        public string Command => "makemerc";

        public string Description => "робить вибраного гравця найманцем";

        public string Help => "makemerc <uid>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player != null)
            {
                shell.WriteLine("You cannot use this command from the player.");
                return;
            }

            if (args.Length != 1)
            {
                shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!int.TryParse(args[0], out var intUid))
            {
                shell.WriteError("uid must be a number");
                return;
            }

            var targetNet = new NetEntity(intUid);

            if (!EntityManager.TryGetEntity(targetNet, out var target))
            {
                shell.WriteError("cannot find entity");
                return;
            }

            if (_playerManager.TryGetSessionByEntity(target.Value, out var session) &&
                _banManager.IsRoleBanned(session, MercenaryBan))
            {
                shell.WriteError("cannot make mercenary: player is banned from the Mercenary antagonist role");
                return;
            }

            EntityManager.System<MakeAMercSystem>().MakeAnMerc(target.Value);
        }
    }
}
