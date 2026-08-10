// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Atmos.HFR;
using Content.Pirate.Shared.Atmos.HFR;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Server.Administration.Commands;

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
        EntityUid? target = null;

        if (args.Length >= 1 && NetEntity.TryParse(args[0], out var net) && _entManager.TryGetEntity(net, out var uid))
        {
            target = uid;
        }
        else
        {
            // No uid given: grab the first HFR that exists in the game.
            var query = _entManager.EntityQueryEnumerator<HFRComponent>();
            if (query.MoveNext(out var uid2, out _))
                target = uid2;
        }

        if (target is not { } hfr || !_entManager.TryGetComponent<HFRComponent>(hfr, out var comp))
        {
            shell.WriteError("Could not find an HFR reactor. Pass a uid or place one in the world first.");
            return;
        }

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
        EntityUid? target = null;

        if (args.Length >= 1 && NetEntity.TryParse(args[0], out var net) && _entManager.TryGetEntity(net, out var uid))
        {
            target = uid;
        }
        else
        {
            var query = _entManager.EntityQueryEnumerator<HFRComponent>();
            if (query.MoveNext(out var uid2, out _))
                target = uid2;
        }

        if (target is not { } hfr || !_entManager.TryGetComponent<HFRComponent>(hfr, out var comp))
        {
            shell.WriteError("Could not find an HFR reactor. Pass a uid or place one in the world first.");
            return;
        }

        _entManager.System<HFRSystem>().ResetReactor((hfr, comp));
        shell.WriteLine($"HFR {hfr} reset: meltdown stopped, integrity restored to 100%.");
    }
}
