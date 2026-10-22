// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Voting;

public sealed class PirateVotingSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IVoteManager _votes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly PirateVoteHistory _history = new();

    public PirateVotingPrototype Configuration => _prototypes.Index<PirateVotingPrototype>(PirateVotingPrototype.DefaultId);

    public TimeSpan MinimumPlayedTime => TimeSpan.FromMinutes(Math.Max(0, _cfg.GetCVar(CCVars.VoteMinimumPlayedMinutes)));

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.Old == GameRunLevel.InRound && ev.New == GameRunLevel.InRound)
            return;

        // Also check when a round ends, before its duration is reset.
        if (ev.Old == GameRunLevel.InRound)
            RecordPlayedRound();

        _history.BeginRound(ev.New == GameRunLevel.InRound ? _ticker.CurrentPreset?.ID : null);
    }

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        if (_votes is VoteManager manager)
            manager.CancelPirateSelectionVotes();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_ticker.RunLevel == GameRunLevel.InRound)
            RecordPlayedRound();
    }

    private void RecordPlayedRound()
    {
        if (!_history.HasPendingRound)
            return;

        var config = Configuration;
        var historyLength = config.ModePenalties.Values.Select(p => p.Count).DefaultIfEmpty(2).Max();
        _history.TryRecord(_ticker.RoundDuration(), MinimumPlayedTime, historyLength);
    }

    public float GetPenalty(string preset)
    {
        return Configuration.ModePenalties.TryGetValue(preset, out var penalties)
            ? _history.GetPenalty(preset, penalties)
            : 0f;
    }
}
