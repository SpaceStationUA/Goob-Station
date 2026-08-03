using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using DiagnosticStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._Pirate.ZLevels.Core;

/// <summary>
/// Per-tick count + wallclock budget for Z-level transitions. Map-crossing moves call
/// <see cref="CanProcessZLevelTransition"/> first; once the budget is spent the rest defer to the
/// next tick.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    private int _maxZTransitionsPerTick = 64;
    private TimeSpan _zTransitionBudget = TimeSpan.FromMilliseconds(1);
    private GameTick _zTransitionBudgetTick;
    private int _zTransitionsThisTick;
    private long _zTransitionBudgetStart;

    private void InitTransitionBudget()
    {
        Subs.CVar(_config, CCVars.CEZMaxTransitionsPerTick, value => _maxZTransitionsPerTick = Math.Max(0, value), true);
        Subs.CVar(_config, CCVars.CEZTransitionBudgetMs, value => _zTransitionBudget = TimeSpan.FromMilliseconds(Math.Max(0f, value)), true);
    }

    protected override bool CanProcessZLevelTransition(EntityUid ent, int offset)
    {
        if (_maxZTransitionsPerTick <= 0)
            return false;

        var curTick = _timing.CurTick;
        // Re-establish the wallclock baseline when unset, else the first call (default tick value)
        // reads a zero timestamp into GetElapsedTime and instantly exhausts the budget.
        if (_zTransitionBudgetTick != curTick || _zTransitionBudgetStart == 0)
        {
            _zTransitionBudgetTick = curTick;
            _zTransitionsThisTick = 0;
            _zTransitionBudgetStart = DiagnosticStopwatch.GetTimestamp();
        }

        if (_zTransitionsThisTick >= _maxZTransitionsPerTick)
            return false;

        if (_zTransitionBudget > TimeSpan.Zero &&
            DiagnosticStopwatch.GetElapsedTime(_zTransitionBudgetStart) >= _zTransitionBudget)
        {
            return false;
        }

        _zTransitionsThisTick++;
        return true;
    }
}
