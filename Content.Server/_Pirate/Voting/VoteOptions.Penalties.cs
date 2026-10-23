// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Voting;

public sealed partial class VoteOptions
{
    public Dictionary<object, float> PiratePenalties { get; } = new();
}
