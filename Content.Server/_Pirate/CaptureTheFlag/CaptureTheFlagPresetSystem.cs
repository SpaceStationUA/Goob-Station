// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Content.Server.GameTicking;

namespace Content.Server._Pirate.CaptureTheFlag;

public sealed class CaptureTheFlagPresetSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly CaptureTheFlagRuleSystem _rules = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStartAttempt);
    }

    private void OnRoundStartAttempt(RoundStartAttemptEvent args)
    {
        if (_gameTicker.CurrentPreset?.ID != CaptureTheFlagRuleSystem.PresetId)
            return;

        if (!_rules.TryGetActiveArena(out _, out _))
        {
            _chatManager.SendAdminAnnouncement(Loc.GetString("ctf-invalid-arena"));
            args.CancelWithoutFallback();
        }
        else if (!args.Forced && args.Players.Length < 2)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("ctf-insufficient-ready"));
            args.CancelWithoutFallback();
        }
    }
}
