using Content.Pirate.Server.Traits.Vampirism;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;

namespace Content.Server.Administration.Toolshed;

/// <summary>
/// Debug command that issues a vampire certificate to a player, the same way it is issued on spawn.
/// </summary>
[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SpawnVampireCertificateCommand : ToolshedCommand
{
    private VampireCertificateSpawnSystem? _certificateSystem;
    private GameTicker? _ticker;

    [CommandImplementation]
    public IEnumerable<EntityUid> SpawnVampireCertificate([PipedArgument] IEnumerable<EntityUid> input)
    {
        _certificateSystem ??= GetSys<VampireCertificateSpawnSystem>();
        _ticker ??= GetSys<GameTicker>();

        foreach (var i in input)
        {
            if (!TryComp(i, out ActorComponent? targetActor))
                continue;

            var profile = _ticker.GetPlayerProfile(targetActor.PlayerSession);
            _certificateSystem.IssueCertificate(i, profile);
            yield return i;
        }
    }

    [CommandImplementation]
    public void SpawnVampireCertificate(IInvocationContext ctx)
    {
        _certificateSystem ??= GetSys<VampireCertificateSpawnSystem>();
        _ticker ??= GetSys<GameTicker>();

        if (ExecutingEntity(ctx) is not { } ent)
        {
            if (ctx.Session is { } session)
                ctx.ReportError(new SessionHasNoEntityError(session));
            else
                ctx.ReportError(new NotForServerConsoleError());
            return;
        }

        if (!TryComp(ent, out ActorComponent? targetActor))
            return;

        var profile = _ticker.GetPlayerProfile(targetActor.PlayerSession);
        _certificateSystem.IssueCertificate(ent, profile);
    }
}
