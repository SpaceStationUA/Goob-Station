// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server.Voting;

public sealed partial class VoteOptions
{
    public Dictionary<object, VoteOptionVisuals> OptionVisuals { get; } = new();
}

public readonly record struct VoteOptionVisuals(string? Icon, EntProtoId? Preview);
