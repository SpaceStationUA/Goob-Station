// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;

namespace Content.Server.GameTicking;

/// <summary>
/// Tracks *played* game presets, not lobby selections or failed starts.
/// Two consecutive SecretSalt rounds temporarily block another SecretSalt round.
/// </summary>
public sealed class SecretSaltRotationTracker
{
    public const string PresetId = "SecretSalt";
    public const int MaximumConsecutiveRounds = 2;

    private int _lastRecordedRoundId;
    public int ConsecutiveRounds { get; private set; }
    public bool IsBlocked => ConsecutiveRounds >= MaximumConsecutiveRounds;

    public bool RecordPlayedRound(int roundId, string? preset)
    {
        if (roundId <= 0 || roundId == _lastRecordedRoundId)
            return false;

        // A failed attempt must never affect the rotation. Only the ticker's
        // transition into InRound calls this method.
        _lastRecordedRoundId = roundId;
        ConsecutiveRounds = preset == PresetId
            ? System.Math.Min(MaximumConsecutiveRounds, ConsecutiveRounds + 1)
            : 0;
        return true;
    }
}

/// <summary>
/// Listens to the authoritative GameTicker transition that means a round
/// actually started, even when the preset was changed by a fallback.
/// </summary>
public sealed class SecretSaltRotationSystem : EntitySystem
{
    private readonly SecretSaltRotationTracker _tracker = new();

    public int ConsecutiveRounds => _tracker.ConsecutiveRounds;
    public bool IsBlocked => _tracker.IsBlocked;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound || ev.Old == GameRunLevel.InRound)
            return;

        var ticker = EntityManager.System<GameTicker>();
        _tracker.RecordPlayedRound(ticker.RoundId, ticker.CurrentPreset?.ID);
    }
}
