// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    private void SendAdminVoteResults(VoteReg vote)
    {
        var optionResults = vote.Entries.Select((entry, optionId) =>
        {
            var voters = vote.CastVotes
                .Where(castVote => castVote.Value == optionId)
                .Select(castVote => castVote.Key.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var voterNames = voters.Length == 0
                ? Loc.GetString("vote-manager-admin-results-no-voters")
                : string.Join(", ", voters);

            return Loc.GetString("vote-manager-admin-results-option",
                ("option", entry.Text),
                ("voters", voterNames));
        });

        var title = Loc.GetString("vote-manager-admin-results-title", ("title", vote.Title));
        _chatManager.SendAdminAlert($"{title}\n{string.Join("\n", optionResults)}");
    }
}
