// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Server._Pirate.Voting;

/// <summary>
/// Pure ballot rules. Ties at the finalist boundary use a caller-supplied shuffled order.
/// </summary>
public static class PirateVoteRules
{
    public static int SubtractedVotes(int votes, float penalty)
    {
        return (int) Math.Floor(votes * (double) Math.Clamp(penalty, 0f, 1f));
    }

    public static int EffectiveVotes(int votes, float penalty)
    {
        return votes - SubtractedVotes(votes, penalty);
    }

    public static object[] SelectFinalists(
        IEnumerable<(object Option, int Votes)> shuffledOptions,
        int finalistCount)
    {
        return shuffledOptions
            .Where(option => option.Votes > 0)
            .OrderByDescending(option => option.Votes)
            .Take(Math.Max(1, finalistCount))
            .Select(option => option.Option)
            .ToArray();
    }
}

/// <summary>
/// Only qualified rounds advance history. Ballots never change it.
/// </summary>
public sealed class PirateVoteHistory
{
    private readonly List<string> _played = new();
    private string? _pendingPreset;

    public bool HasPendingRound => _pendingPreset != null;

    public void BeginRound(string? preset)
    {
        _pendingPreset = preset;
    }

    public bool TryRecord(TimeSpan elapsed, TimeSpan minimum, int historyLength)
    {
        if (_pendingPreset == null || elapsed < minimum)
            return false;

        _played.Insert(0, _pendingPreset);
        _pendingPreset = null;
        if (_played.Count > historyLength)
            _played.RemoveRange(historyLength, _played.Count - historyLength);
        return true;
    }

    public float GetPenalty(string preset, IReadOnlyList<float> penalties)
    {
        var index = _played.IndexOf(preset);
        return index >= 0 && index < penalties.Count ? Math.Clamp(penalties[index], 0f, 1f) : 0f;
    }
}
