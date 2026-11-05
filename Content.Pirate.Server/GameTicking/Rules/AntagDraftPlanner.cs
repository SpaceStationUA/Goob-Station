// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Server.GameTicking.Rules.Components;

namespace Content.Pirate.Server.GameTicking.Rules;

public readonly record struct AntagDraftPick(string Rule, int Antags, bool Major);

public sealed record AntagDraftPlan(int Players, float PlayersPerAntag, int Target, IReadOnlyList<AntagDraftPick> Picks);

public static class AntagDraftPlanner
{
    public static AntagDraftPlan Plan(AntagDraftComponent draft,
        int players,
        Func<string, int> minPlayers,
        Func<string, IEnumerable<string>?> incompatible,
        Func<double> nextDouble)
    {
        var low = Math.Min(draft.PlayersPerAntagMin, draft.PlayersPerAntagMax);
        var high = Math.Max(draft.PlayersPerAntagMin, draft.PlayersPerAntagMax);
        var playersPerAntag = MathF.Max(1f, low + (float) nextDouble() * (high - low));

        var target = (int) Math.Round(players / playersPerAntag, MidpointRounding.AwayFromZero);
        target = Math.Max(target, draft.MinAntags);
        if (draft.MaxAntags > 0)
            target = Math.Min(target, draft.MaxAntags);

        var antags = new Dictionary<string, int>();
        var order = new List<string>();
        var picks = new Dictionary<string, int>();
        var excluded = new HashSet<string>();
        var slots = target;
        var first = true;
        string? major = null;

        while (slots > 0)
        {
            var list = first ? draft.FirstPick : draft.Picks;
            var candidates = list.Where(e =>
                    e.Weight > 0
                    && !excluded.Contains(e.Rule)
                    && picks.GetValueOrDefault(e.Rule) < e.MaxPicks
                    && Math.Max(minPlayers(e.Rule), e.MinPlayers) <= players
                    && (e.Major ? first : e.Antags <= slots))
                .ToList();

            if (candidates.Count == 0)
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                break;
            }

            var pick = PickWeighted(candidates, nextDouble);
            picks[pick.Rule] = picks.GetValueOrDefault(pick.Rule) + 1;

            if (pick.Major)
            {
                major = pick.Rule;
                break;
            }

            if (!antags.ContainsKey(pick.Rule))
                order.Add(pick.Rule);
            antags[pick.Rule] = antags.GetValueOrDefault(pick.Rule) + pick.Antags;
            slots -= pick.Antags;

            if (incompatible(pick.Rule) is { } modes)
                excluded.UnionWith(modes);

            first = false;
        }

        var result = new List<AntagDraftPick>();
        if (major != null)
            result.Add(new AntagDraftPick(major, 0, true));
        result.AddRange(order.Select(rule => new AntagDraftPick(rule, antags[rule], false)));
        return new AntagDraftPlan(players, playersPerAntag, target, result);
    }

    private static AntagDraftEntry PickWeighted(List<AntagDraftEntry> entries, Func<double> nextDouble)
    {
        var total = entries.Sum(e => e.Weight);
        var roll = nextDouble() * total;
        foreach (var entry in entries)
        {
            roll -= entry.Weight;
            if (roll <= 0)
                return entry;
        }

        return entries[^1];
    }
}
