// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Pirate.Instruments;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Instruments;

/// <summary>Debug command for auditioning sampled songs.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class PlaySampledSongCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "playsampledsong";
    public string Description => "Plays a sampled song prototype out of an entity, using its sample bank.";
    public string Help => $"{Command} <songId> [entityUid] [range] [volume]\n" +
                          $"{Command} list - shows every song id";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0 || args[0] == "list")
        {
            var ids = _proto.EnumeratePrototypes<SampledSongPrototype>()
                .Select(s => $"  {s.ID} ({s.Notes.Count} notes, {s.Duration:0.0}s, bank {s.Bank})")
                .OrderBy(s => s);

            shell.WriteLine(string.Join('\n', ids.DefaultIfEmpty("  (none)")));
            return;
        }

        if (!_proto.HasIndex<SampledSongPrototype>(args[0]))
        {
            shell.WriteError($"No sampled song called '{args[0]}'. Try '{Command} list'.");
            return;
        }

        EntityUid source;
        if (args.Length > 1)
        {
            if (!NetEntity.TryParse(args[1], out var net) || !_entities.TryGetEntity(net, out var parsed))
            {
                shell.WriteError($"'{args[1]}' is not an entity uid.");
                return;
            }

            source = parsed.Value;
        }
        else if (shell.Player?.AttachedEntity is { } attached)
        {
            source = attached;
        }
        else
        {
            shell.WriteError("No entity given and you are not attached to one.");
            return;
        }

        float? range = null;
        if (args.Length > 2)
        {
            if (!float.TryParse(args[2], CultureInfo.InvariantCulture, out var parsedRange))
            {
                shell.WriteError($"'{args[2]}' is not a number.");
                return;
            }

            range = parsedRange;
        }

        float? volume = null;
        if (args.Length > 3)
        {
            if (!float.TryParse(args[3], CultureInfo.InvariantCulture, out var parsedVolume))
            {
                shell.WriteError($"'{args[3]}' is not a number.");
                return;
            }

            volume = parsedVolume;
        }

        var system = _entities.System<SampledSongSystem>();
        if (!system.TryPlaySong(source, args[0], range, volume))
        {
            shell.WriteError($"Failed to start '{args[0]}'.");
            return;
        }

        shell.WriteLine($"Playing '{args[0]}' from {_entities.ToPrettyString(source)}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _proto.EnumeratePrototypes<SampledSongPrototype>().Select(s => s.ID).Order(),
                "<songId>");
        }

        return args.Length switch
        {
            2 => CompletionResult.FromHint("[entityUid]"),
            3 => CompletionResult.FromHint("[range]"),
            4 => CompletionResult.FromHint("[volume]"),
            _ => CompletionResult.Empty,
        };
    }
}
