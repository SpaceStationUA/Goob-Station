// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Atmos.HFR;
using Content.Pirate.Shared.Atmos.HFR;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Server.Administration.Commands;

/// <summary>
///     Shared reactor lookup for the HFR admin commands. When the first argument
///     is a uid it must parse and resolve to an entity with an HFR component — a
///     bad uid is an error, never a silent fallback. Only with no argument at all
///     does the lookup fall back to the first HFR in the game.
/// </summary>
internal static class HFRCommandHelper
{
    public static Entity<HFRComponent>? ResolveHfr(IConsoleShell shell, IEntityManager entManager, string[] args)
    {
        EntityUid? target = null;

        if (args.Length >= 1)
        {
            if (!NetEntity.TryParse(args[0], out var net) || !entManager.TryGetEntity(net, out var uid))
            {
                shell.WriteError($"Could not find an HFR reactor with uid '{args[0]}'. Check the uid and try again.");
                return null;
            }

            target = uid;
        }
        else
        {
            // No uid given: grab the first HFR that exists in the game.
            var query = entManager.EntityQueryEnumerator<HFRComponent>();
            if (query.MoveNext(out var uid2, out _))
                target = uid2;
        }

        if (target is not { } hfr || !entManager.TryGetComponent<HFRComponent>(hfr, out var comp))
        {
            shell.WriteError("Could not find an HFR reactor. Pass a uid or place one in the world first.");
            return null;
        }

        return (hfr, comp);
    }
}

/// <summary>
///     Forces the Hyper-torus Fusion Reactor into a meltdown countdown, for testing
///     the siren, countdown warnings and the rescue gameplay. Runs through the normal
///     atmos loop, so the reactor must be assembled and connected to pipes.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class HFREnableMeltdownCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "hfr-meltdown";
    public string Description => "Forces the HFR into a meltdown countdown (for testing).";
    public string Help => "Usage: hfr-meltdown [uid]  — if no uid is given, uses the first HFR in the game.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (HFRCommandHelper.ResolveHfr(shell, _entManager, args) is not { } reactor)
            return;

        var hfr = reactor.Owner;
        var comp = reactor.Comp;

        // Drop integrity below the melting threshold. The next atmos tick will start
        // a fresh countdown with all the effects (siren, radio, station announcement
        // for critical recipes), giving the player a chance to rescue it.
        comp.Integrity = 1f;
        comp.MeltdownCountdownActive = false;
        comp.MeltdownCountdown = 0f;
        comp.MeltdownCriticalSoundPlayed = false;
        _entManager.Dirty(hfr, comp);

        shell.WriteLine($"Meltdown countdown started on {hfr}. You have {HFRConstants.MeltdownCountdownTime:0} seconds to rescue it.");
    }
}

/// <summary>
///     Resets a Hyper-torus Fusion Reactor to a pristine state: stops the meltdown
///     countdown and siren, restores full integrity and clears iron buildup.
///     Use after <c>hfr-meltdown</c> to re-test the rescue gameplay.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class HFRResetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "hfr-reset";
    public string Description => "Resets an HFR reactor to a pristine state (stops meltdown, restores integrity).";
    public string Help => "Usage: hfr-reset [uid]  — if no uid is given, uses the first HFR in the game.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (HFRCommandHelper.ResolveHfr(shell, _entManager, args) is not { } reactor)
            return;

        _entManager.System<HFRSystem>().ResetReactor(reactor);
        shell.WriteLine($"HFR {reactor.Owner} reset: meltdown stopped, integrity restored to 100%.");
    }
}
