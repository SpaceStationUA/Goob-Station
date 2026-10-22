// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Voting;
using Robust.Shared.Random;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    private readonly HashSet<int> _pirateSelectionVotes = new();

    public TimeSpan PirateSelectionTimeLeft => _pirateSelectionVotes
        .Where(id => _votes.TryGetValue(id, out var vote) && !vote.Finished)
        .Select(id => _votes[id].EndTime - _timing.RealTime)
        .DefaultIfEmpty(TimeSpan.Zero)
        .Max();

    public void CancelPirateSelectionVotes()
    {
        foreach (var id in _pirateSelectionVotes.ToArray())
        {
            if (_voteHandles.TryGetValue(id, out var vote))
                vote.Cancel();
        }
        _pirateSelectionVotes.Clear();
    }

    private static int GetPirateEffectiveVotes(VoteEntry entry)
    {
        return PirateVoteRules.EffectiveVotes(entry.Votes, entry.PiratePenalty);
    }

    private void CreatePirateSelectionVote(VoteOptions options, VoteFinishedEventHandler onFinished, bool final = false)
    {
        if (options.Options.Count == 0)
            return;

        var vote = CreateVote(options);
        _pirateSelectionVotes.Add(vote.Id);
        vote.OnCancelled += handle => _pirateSelectionVotes.Remove(handle.Id);
        vote.OnFinished += (handle, args) =>
        {
            _pirateSelectionVotes.Remove(handle.Id);
            var finalistCount = _entityManager.System<PirateVotingSystem>().Configuration.FinalistCount;
            if (!final && finalistCount > 1)
            {
                var ranked = handle.VotesPerOption.Select(pair => (Option: pair.Key, Votes: pair.Value)).ToList();
                _random.Shuffle(ranked);
                var finalists = PirateVoteRules.SelectFinalists(ranked, finalistCount);
                if (finalists.Length > 1 && finalists.Length < options.Options.Count)
                {
                    var runoff = new VoteOptions
                    {
                        Title = options.Title,
                        Duration = options.Duration,
                        InitiatorText = options.InitiatorText,
                        // Do not extend the player's call timeout for the automatic ballot.
                        VoterEligibility = options.VoterEligibility,
                        DisplayVotes = options.DisplayVotes,
                    };
                    foreach (var finalist in finalists)
                    {
                        runoff.Options.Add(options.Options.First(option => Equals(option.data, finalist)));
                        if (options.OptionVisuals.TryGetValue(finalist, out var visual))
                            runoff.OptionVisuals[finalist] = visual;
                        if (options.PiratePenalties.TryGetValue(finalist, out var penalty))
                            runoff.PiratePenalties[finalist] = penalty;
                    }

                    CreatePirateSelectionVote(runoff, onFinished, true);
                    return;
                }
            }

            onFinished(handle, args);
        };
    }

    private void AddPiratePresetPenalties(VoteOptions options)
    {
        var system = _entityManager.System<PirateVotingSystem>();
        foreach (var (_, data) in options.Options)
        {
            if (data is string preset)
                options.PiratePenalties[data] = system.GetPenalty(preset);
        }
    }
}
