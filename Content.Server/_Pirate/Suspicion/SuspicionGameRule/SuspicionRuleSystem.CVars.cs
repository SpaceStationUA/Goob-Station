// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.CCVar;

namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private void InitializeCVars()
    {
        Subs.CVar(_cfg, CCVars.SSSTraitorPercentage, f => _traitorPercentage = f, true);
        Subs.CVar(_cfg, CCVars.SSSDetectivePercentage, f => _detectivePercentage = f, true);
        Subs.CVar(_cfg, CCVars.SSSWildcardPercentage, f => _wildcardPercentage = f, true);
        Subs.CVar(_cfg, CCVars.SSSWildcardChance, f => _wildcardChance = f, true);
        Subs.CVar(_cfg, CCVars.SSSPreparingDuration, i => _preparingDuration = i, true);
        Subs.CVar(_cfg, CCVars.SSSRoundDuration, i => _roundDuration = i, true);
        Subs.CVar(_cfg, CCVars.SSSTimeAddedPerKill, i => _timeAddedPerKill = i, true);
        Subs.CVar(_cfg, CCVars.SSSPostRoundDuration, i => _postRoundDuration = i, true);
    }

    private float _traitorPercentage = 0.25f;
    private float _detectivePercentage = 0.25f;
    private float _wildcardPercentage = 0.15f;
    private float _wildcardChance = 0.3f;
    private int _preparingDuration = 30;
    private int _roundDuration = 480;
    private int _timeAddedPerKill = 30;
    private int _postRoundDuration = 30;
}
