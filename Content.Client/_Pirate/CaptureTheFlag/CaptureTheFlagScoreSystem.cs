// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CaptureTheFlag;
using Content.Shared.GameTicking;

namespace Content.Client._Pirate.CaptureTheFlag;

public sealed class CaptureTheFlagScoreSystem : EntitySystem
{
    public event Action<CaptureTheFlagScoreEvent>? ScoreChanged;
    public event Action? ScoreClosed;
    public event Action<bool?>? AssignmentChanged;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CaptureTheFlagScoreEvent>(score => ScoreChanged?.Invoke(score));
        SubscribeNetworkEvent<CaptureTheFlagAssignmentEvent>(ev => AssignmentChanged?.Invoke(ev.Red));
        SubscribeNetworkEvent<CaptureTheFlagScoreClosedEvent>(_ => ScoreClosed?.Invoke());
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ScoreClosed?.Invoke());
    }
}
