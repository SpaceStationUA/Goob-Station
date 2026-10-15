// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared._Pirate.Journey;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Journey;

/// <summary>
///     Admin controls for the journey mode: start and stop the rule, jump to a sector, and limit which
///     sectors the round may enter.
/// </summary>
/// <remarks>
///     Derives from LocalizedEntityCommands like the game-ticking commands. It only lives while the entity
///     system is active, so it can depend on the ticker and the journey system.
/// </remarks>
[AdminCommand(AdminFlags.Fun)]
public sealed class JourneyCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly JourneyRuleSystem _journey = default!;

    public override string Command => "journey";
    public override string Description => "Journey mode control: sectors, jumps, restrictions.";
    public override string Help =>
        "journey start | journey stop | journey status | journey sectors | " +
        "journey jump <sector> | journey force <sector> | journey allow <sector> | journey allowall | " +
        "journey preset <calm|standard|dynamic>\n" +
        "  start            begin the journey rule for this round\n" +
        "  stop             end the journey rule for this round\n" +
        "  status           which sector the station sits in, plus restrictions\n" +
        "  sectors          list every sector with its weight and majors\n" +
        "  jump <sector>    jump to a sector (warps every client, announces, applies its rules)\n" +
        "  force <sector>   jump ignoring the round's allowed-sector list\n" +
        "  allow <sector>   restrict the round to this sector; repeat to widen the gate\n" +
        "  allowall         clear the restriction\n" +
        "  preset calm      gate the round to quiet sectors (no majors)\n" +
        "  preset dynamic   gate the round to loud sectors (with majors)\n" +
        "  preset standard  clear the restriction";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0])
        {
            case "start":
                Start(shell);
                break;
            case "stop":
                Stop(shell);
                break;
            case "status":
                Status(shell);
                break;
            case "sectors":
                Sectors(shell);
                break;
            case "jump":
            case "force":
                Jump(shell, args, force: args[0] == "force");
                break;
            case "allow":
                Allow(shell, args);
                break;
            case "preset":
                Preset(shell, args);
                break;
            case "allowall":
                if (TryGetRule(shell, out var rule))
                {
                    EntityManager.GetComponent<JourneyRuleComponent>(rule).AllowedSectors.Clear();
                    shell.WriteLine("the round is free to enter any sector");
                }
                break;
            default:
                shell.WriteLine($"unknown journey action '{args[0]}'");
                break;
        }
    }

    private bool TryFindRule(out EntityUid rule)
    {
        foreach (var comp in EntityManager.EntityQuery<JourneyRuleComponent>(true))
        {
            rule = comp.Owner;
            return true;
        }

        rule = default;
        return false;
    }

    private bool TryGetRule(IConsoleShell shell, out EntityUid rule)
    {
        if (TryFindRule(out rule))
            return true;

        shell.WriteLine("the journey rule is not running; 'journey start' first");
        return false;
    }

    private void Start(IConsoleShell shell)
    {
        // Adding a rule is not starting it: an added rule never runs Started(), so every jump would be
        // refused. Start the existing entity if there is one, because a second rule entity would be dead.
        if (!TryFindRule(out var rule))
        {
            shell.WriteLine(_ticker.StartGameRule("JourneyRule")
                ? "journey rule started"
                : "journey rule could not start");
            return;
        }

        if (_ticker.IsGameRuleActive(rule))
        {
            shell.WriteLine("the journey rule is already running");
            return;
        }

        shell.WriteLine(_ticker.StartGameRule(rule)
            ? "journey rule started"
            : "journey rule could not start");
    }

    private void Stop(IConsoleShell shell)
    {
        if (!TryGetRule(shell, out var rule))
            return;

        _ticker.EndGameRule(rule);
        shell.WriteLine("journey rule ended");
    }

    private void Status(IConsoleShell shell)
    {
        if (!TryGetRule(shell, out var rule))
            return;

        var comp = EntityManager.GetComponent<JourneyRuleComponent>(rule);
        var active = EntityManager.TryGetComponent<GameRuleComponent>(rule, out var gameRule)
            && _ticker.IsGameRuleActive(rule, gameRule);
        var current = comp.Sector == string.Empty ? "(none)" : comp.Sector;
        var allowed = comp.AllowedSectors.Count == 0
            ? "any"
            : string.Join(", ", comp.AllowedSectors);
        shell.WriteLine($"sector: {current}\nallowed: {allowed}\nactive: {(active ? "yes" : "NO, restart the rule")}");

        // The pending jump shows the countdown. If it reaches zero with no arrival call, the delayed apply is broken.
        if (comp.PendingSector != string.Empty)
        {
            var remaining = comp.ApplyAt - _ticker.RoundDuration();
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            shell.WriteLine($"pending: {comp.PendingSector} (applies in ~{remaining.TotalSeconds:0}s)");
        }

        var ruleCount = 0;
        foreach (var _ in EntityManager.EntityQuery<JourneyRuleComponent>(true))
            ruleCount++;
        if (ruleCount > 1)
            shell.WriteLine($"WARNING: {ruleCount} journey rule entities (duplicate `journey start`?)");

        if (comp.Sector == string.Empty || !_prototypes.TryIndex<JourneySectorPrototype>(comp.Sector, out var sector))
            return;

        var dwell = _ticker.RoundDuration() - comp.SectorEnteredAt;
        if (dwell < TimeSpan.Zero)
            dwell = TimeSpan.Zero;

        if (sector.MajorEvents.Count == 0)
        {
            shell.WriteLine($"in sector: {dwell.TotalMinutes:0.#} min, quiet sector (no majors)");
            return;
        }

        var majors = string.Join(", ", sector.MajorEvents);
        var remainingGrace = sector.MajorDelayMinutes - (float) dwell.TotalMinutes;
        shell.WriteLine(remainingGrace > 0
            ? $"in sector: {dwell.TotalMinutes:0.#} min, majors [{majors}] open in ~{remainingGrace:0.#} min"
            : $"in sector: {dwell.TotalMinutes:0.#} min, majors [{majors}] eligible (past arrival grace)");
    }

    private void Sectors(IConsoleShell shell)
    {
        foreach (var sector in _prototypes.EnumeratePrototypes<JourneySectorPrototype>())
        {
            var mood = sector.MajorEvents.Count == 0
                ? "quiet"
                : $"majors: {string.Join(", ", sector.MajorEvents)}";
            shell.WriteLine($"{sector.ID}: {Loc.GetString(sector.Name)} (parallax {sector.Parallax}, weight {sector.Weight:0.##}, {mood})");
        }
    }

    private void Jump(IConsoleShell shell, string[] args, bool force)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("name a sector: journey jump <sector>");
            return;
        }

        if (!TryGetRule(shell, out var rule))
            return;

        var comp = EntityManager.GetComponent<JourneyRuleComponent>(rule);
        var refusal = _journey.Jump(rule, comp, args[1], force);
        shell.WriteLine(refusal ?? $"jumped to {args[1]}");
    }

    private void Preset(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("name a preset: journey preset <calm|standard|dynamic>");
            return;
        }

        if (!TryGetRule(shell, out var rule))
            return;

        var comp = EntityManager.GetComponent<JourneyRuleComponent>(rule);
        switch (args[1])
        {
            case "calm":
                SetAllowed(comp, sector => sector.MajorEvents.Count == 0);
                shell.WriteLine($"calm round: {string.Join(", ", comp.AllowedSectors)}");
                break;
            case "dynamic":
                SetAllowed(comp, sector => sector.MajorEvents.Count > 0);
                shell.WriteLine($"dynamic round: {string.Join(", ", comp.AllowedSectors)}");
                break;
            case "standard":
                comp.AllowedSectors.Clear();
                shell.WriteLine("the round is free to enter any sector");
                break;
            default:
                shell.WriteLine($"unknown journey preset '{args[1]}'");
                break;
        }
    }

    private void SetAllowed(JourneyRuleComponent comp, Func<JourneySectorPrototype, bool> filter)
    {
        comp.AllowedSectors.Clear();
        foreach (var sector in _prototypes.EnumeratePrototypes<JourneySectorPrototype>())
        {
            if (filter(sector))
                comp.AllowedSectors.Add(sector.ID);
        }
    }

    private void Allow(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("name a sector: journey allow <sector>");
            return;
        }

        if (!TryGetRule(shell, out var rule))
            return;

        if (!_prototypes.TryIndex<JourneySectorPrototype>(args[1], out var sector))
        {
            shell.WriteLine($"no journey sector called '{args[1]}'");
            return;
        }

        var comp = EntityManager.GetComponent<JourneyRuleComponent>(rule);
        if (!comp.AllowedSectors.Contains(sector.ID))
            comp.AllowedSectors.Add(sector.ID);

        shell.WriteLine($"the round's sectors: {string.Join(", ", comp.AllowedSectors)}");
    }
}
