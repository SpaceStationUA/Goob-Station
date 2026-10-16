#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.EntityTable.Conditions;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.IntegrationTests.Tests._Pirate.GameModeSim;

public sealed class GameModeSimulator
{
    private const double TickSeconds = 1;
    private const double PopulationStepSeconds = 10;

    private readonly GameModeSimData _data;
    private readonly GameModeSimConfig _cfg;
    private readonly GameModeSimOutput _out;
    private readonly HashSet<string> _warned = new();

    private Random _rand = new();

    // Share population randomness across presets; keep scheduler and antag picks independent.
    private Random _popRand = new();
    private GameModeSimScenario _scenario = new();
    private string _presetId = "";
    private int _round;
    private double _now;
    private bool _lobby;
    private bool _roundEndRequested;
    private double _glimmer;
    private readonly List<SimPerson> _people = new();
    private readonly List<(double Time, string Id)> _history = new();
    private readonly List<RuleInstance> _rules = new();
    private readonly List<ChaosState> _chaos = new();
    private readonly List<BasicState> _basic = new();
    private ChaosState? _tpOwner;
    private readonly Dictionary<RuleInstance, int> _tpReservations = new();
    private readonly List<PendingGhostRole> _pendingGhostRoles = new();
    private RoundSummary _summary = new();
    private RoundParams _p = new();

    private bool _countsDirty = true;
    private int _connected;
    private int _alive;
    private int _ghosts;
    private int _aliveAntags;
    private int _aliveSec;

    public GameModeSimulator(GameModeSimData data, GameModeSimConfig cfg, GameModeSimOutput output)
    {
        _data = data;
        _cfg = cfg;
        _out = output;
    }

    public void RunRound(string presetId, GameModeSimScenario scenario, int round, int seed, int paramSeed)
    {
        _p = RoundParams.Roll(_cfg, scenario, new Random(paramSeed));
        var preset = _data.Presets[presetId];
        _rand = new Random(seed);
        _popRand = new Random(paramSeed ^ 0x51ED270B);
        _scenario = scenario;
        _presetId = presetId;
        _round = round;
        _now = 0;
        _lobby = true;
        _roundEndRequested = false;
        _glimmer = _p.GlimmerStart;
        _people.Clear();
        _history.Clear();
        _rules.Clear();
        _chaos.Clear();
        _basic.Clear();
        _tpReservations.Clear();
        _pendingGhostRoles.Clear();
        _tpOwner = null;
        _summary = new RoundSummary { Seed = seed, Params = _p };
        _countsDirty = true;

        SetupPeople();
        _summary.StartConnected = Connected();
        _summary.SecAtStart = AliveSec();

        foreach (var scheduler in preset.Chaos)
        {
            var state = new ChaosState(scheduler);
            _chaos.Add(state);
            if (scheduler.Tp != null && _tpOwner == null)
                _tpOwner = state;
            ChaosAdded(state);
        }

        foreach (var scheduler in preset.Basic)
            _basic.Add(new BasicState(scheduler));

        _summary.TpStartPoints = _tpOwner != null ? TpTotal() : 0;
        _lobby = false;

        foreach (var basic in _basic)
        {
            var min = basic.Scheduler.MinimumTimeUntilFirstEvent;
            basic.TimeUntilNextEvent = NextFloat(min, min + 120);
        }

        var end = _scenario.RoundMinutes * 60;
        var evacAt = end - _cfg.EvacCallMinutesBeforeEnd * 60;
        var nextTimeline = 0.0;
        var nextPopulation = PopulationStepSeconds;

        for (_now = 0; _now < end; _now += TickSeconds)
        {
            if (_now >= nextPopulation)
            {
                StepPopulation(PopulationStepSeconds);
                nextPopulation += PopulationStepSeconds;
            }

            if (!_roundEndRequested && _now >= evacAt)
                _roundEndRequested = true;

            UpdateRules();

            foreach (var state in _chaos)
                ChaosTick(state, (float) TickSeconds);

            foreach (var basic in _basic)
                BasicTick(basic, TickSeconds);

            if (_now >= nextTimeline)
            {
                WriteTimeline();
                nextTimeline += _cfg.TimelineStepMinutes * 60;
            }
        }

        FinishRound();
    }

    #region Population

    private void SetupPeople()
    {
        var id = 0;
        for (var i = 0; i < _scenario.RoundstartPlayers; i++)
        {
            var person = NewPerson(id++, 0);
            person.Present = true;
            person.Alive = true;
            person.Job = RollJob();
            _people.Add(person);
        }

        for (var i = 0; i < _p.Observers; i++)
        {
            var person = NewPerson(id++, 0);
            person.Present = true;
            person.Observer = true;
            _people.Add(person);
        }

        // Lobby players count toward minPlayers and starting chaos, but are not crew.
        var lateJoiners = Math.Max(0, _scenario.Population - _scenario.RoundstartPlayers - (int) _p.Observers);
        for (var i = 0; i < lateJoiners; i++)
        {
            var joinAt = _p.JoinWindowMinutes * 60 * Math.Pow(_popRand.NextDouble(), _p.JoinCurve);
            var person = NewPerson(id++, Math.Max(joinAt, PopulationStepSeconds));
            person.Present = true;
            person.InLobby = true;
            _people.Add(person);
        }
    }

    private SimPerson NewPerson(int id, double joinAt)
    {
        return new SimPerson
        {
            Id = id,
            JoinAt = joinAt,
            AntagOptIn = _popRand.NextDouble() < _p.AntagOptInChance,
        };
    }

    private string RollJob()
    {
        var weights = new List<(string, double)>();
        var used = 0.0;
        foreach (var job in _cfg.Jobs)
        {
            used += job.Weight;
            var taken = _people.Count(p => p.Present && p.Job == job.Job);
            if (taken < job.Slots)
                weights.Add((job.Job, InDepartment(job.Job, "Security") ? job.Weight * _p.SecurityWeightMultiplier : job.Weight));
        }

        weights.Add((_cfg.FillerJob, Math.Max(0, _cfg.JobWeightTotal - used)));
        return PickWeighted(weights, _popRand) ?? _cfg.FillerJob;
    }

    private void StepPopulation(double dt)
    {
        var aliveAntags = AliveAntags();
        var aliveSec = AliveSec();
        var crewHazard = _p.CrewDeathPerMinute + _p.CrewDeathPerAntagPerMinute * aliveAntags;
        var antagHazard = _p.AntagDeathPerMinute + _p.AntagDeathPerSecPerMinute * aliveSec;
        var crewDeath = 1 - Math.Exp(-crewHazard * dt / 60);
        var antagDeath = 1 - Math.Exp(-antagHazard * dt / 60);

        foreach (var person in _people)
        {
            if (person.InLobby)
            {
                if (person.JoinAt > _now)
                    continue;

                person.InLobby = false;
                person.Alive = true;
                person.Job = RollJob();
                _countsDirty = true;
                continue;
            }

            if (person.Observer)
                continue;

            if (person.Alive)
            {
                if (_popRand.NextDouble() >= (person.Antag != null ? antagDeath : crewDeath))
                    continue;

                person.Alive = false;
                person.Ghost = true;
                person.DeathTime = _now;
                person.ReviveAt = _popRand.NextDouble() < _p.RevivalChance
                    ? _now + (_cfg.RevivalMinMinutes + _popRand.NextDouble() * (_cfg.RevivalMaxMinutes - _cfg.RevivalMinMinutes)) * 60
                    : -1;
                _summary.Deaths++;
                _countsDirty = true;
            }
            else if (person.ReviveAt >= 0 && _now >= person.ReviveAt)
            {
                person.Alive = true;
                person.Ghost = false;
                person.ReviveAt = -1;
                _countsDirty = true;
            }
        }

        _glimmer = Math.Clamp(_glimmer + _p.GlimmerPerMinute * dt / 60, 0, 1000);
        StepGhostRoles(dt);
    }

    private void RefreshCounts()
    {
        if (!_countsDirty)
            return;

        _connected = _alive = _ghosts = _aliveAntags = _aliveSec = 0;
        foreach (var person in _people)
        {
            if (!person.Present)
                continue;

            _connected++;
            if (person.Observer || person.InLobby)
                continue;

            if (person.Alive)
            {
                _alive++;
                if (person.Antag != null)
                    _aliveAntags++;
                else if (InDepartment(person.Job, "Security"))
                    _aliveSec++;
            }
            else if (person.Ghost)
            {
                _ghosts++;
            }
        }

        _countsDirty = false;
        _summary.PeakAliveAntags = Math.Max(_summary.PeakAliveAntags, _aliveAntags);
        _summary.SecPeak = Math.Max(_summary.SecPeak, _aliveSec);
    }

    private int Connected()
    {
        RefreshCounts();
        return _connected;
    }

    private int TotalPlayers() => Connected() + _cfg.PlayerCountBias;

    private (int Players, int Ghosts) CountActivePlayers()
    {
        RefreshCounts();
        return (_alive + _cfg.PlayerCountBias, _ghosts);
    }

    private int AliveAntags()
    {
        RefreshCounts();
        return _aliveAntags;
    }

    private int AliveSec()
    {
        RefreshCounts();
        return _aliveSec;
    }

    private bool InDepartment(string? job, string department)
    {
        return job != null && _data.JobDepartments.TryGetValue(job, out var departments) && departments.Contains(department);
    }

    #endregion

    #region Rules

    private RuleInstance AddRule(string ruleId, string source, int? players = null)
    {
        var rule = _data.Rules[ruleId];
        var instance = new RuleInstance
        {
            Rule = rule,
            Source = source,
            AddedAt = _now,
            Roundstart = _lobby,
        };

        if (rule.Antags != null)
        {
            foreach (var def in rule.Antags)
            {
                var min = def.MinRange is { } minRange ? _rand.Next(minRange.Min, minRange.Max + 1) : def.Min;
                var max = def.MaxRange is { } maxRange ? _rand.Next(maxRange.Min, maxRange.Max + 1) : def.Max;
                instance.Defs.Add(new DefCount(min, max, def.PlayerRatio));
            }
        }

        var delay = rule.DelayMin is { } delayMin ? _rand.Next(delayMin, rule.DelayMax!.Value + 1) : 0;
        instance.StartAt = _now + delay;
        RecordWorld(instance);
        _rules.Add(instance);
        return instance;
    }

    private void UpdateRules()
    {
        for (var i = 0; i < _rules.Count; i++)
        {
            var instance = _rules[i];
            if (!instance.Started)
            {
                if (instance.StartAt <= _now)
                    StartRuleInstance(instance);
                continue;
            }

            if (instance.Active && instance.EndAt is { } endAt && _now >= endAt)
                instance.Active = false;
        }
    }

    private void StartRuleInstance(RuleInstance instance)
    {
        instance.Started = true;
        instance.Active = true;
        instance.StartedAt = _now;
        _history.Add((_now, instance.Rule.Id));

        if (instance.Rule.Event is { Duration: { } duration } ev)
        {
            var length = ev.MaxDuration is { } maxDuration ? NextDouble(duration, maxDuration) : duration;
            instance.EndAt = _now + length;
        }

        if (instance.Rule.Glimmer is { } glimmer)
            _glimmer = Math.Max(0, _glimmer - _rand.Next(glimmer.BurnLower, glimmer.BurnUpper + 1));

        AssignAntags(instance);
    }

    private bool IsRuleActive(string ruleId)
    {
        foreach (var instance in _rules)
        {
            if (instance.Active && instance.Rule.Id == ruleId)
                return true;
        }

        return false;
    }

    private int Occurrences(string ruleId)
    {
        var count = 0;
        foreach (var (_, id) in _history)
        {
            if (id == ruleId)
                count++;
        }

        return count;
    }

    private double LastRun(string ruleId)
    {
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            if (_history[i].Id == ruleId)
                return _history[i].Time;
        }

        return 0;
    }

    private bool CanRun(SimRule rule, int playerCount, double time, float reoccurrenceMult)
    {
        var ev = rule.Event!;
        var speedup = _cfg.EventSpeedup;

        if (IsRuleActive(rule.Id))
            return false;

        if (ev.MaxOccurrences.HasValue && Occurrences(rule.Id) >= ev.MaxOccurrences.Value)
            return false;

        if (playerCount < ev.MinimumPlayers)
            return false;

        if (time != 0 && time / 60 < ev.EarliestStart / speedup)
            return false;

        var lastRun = LastRun(rule.Id);
        if (lastRun != 0 && time / 60 < ev.ReoccurrenceDelay * reoccurrenceMult / speedup + lastRun / 60)
            return false;

        if (_roundEndRequested && !ev.OccursDuringRoundEnd)
            return false;

        if (rule.Glimmer is { } band && (_glimmer < band.Minimum || _glimmer > band.Maximum))
            return false;

        return true;
    }

    private List<SimRule> LimitedEvents(EntityTableSelector table, bool ignoreTimings, int? playerOverride, double? timeOverride, float reoccurrenceMult)
    {
        var playerCount = playerOverride ?? Connected() + _cfg.PlayerCountBias;
        var time = timeOverride ?? (!ignoreTimings ? _now : 0);

        var result = new List<SimRule>();
        var seen = new HashSet<string>();
        var anyAvailable = false;
        var available = new HashSet<string>();
        foreach (var rule in _data.StationEvents)
        {
            if (!CanRun(rule, playerCount, time, reoccurrenceMult))
                continue;

            anyAvailable = true;
            available.Add(rule.Id);
        }

        if (!anyAvailable)
            return result;

        foreach (var id in Spawns(table))
        {
            if (!seen.Add(id) || !available.Contains(id))
                continue;

            result.Add(_data.Rules[id]);
        }

        return result;
    }

    #endregion

    #region Entity tables

    private IEnumerable<string> Spawns(EntityTableSelector selector)
    {
        if (!CheckConditions(selector))
            yield break;

        var rolls = selector.Rolls.Get(_rand);
        for (var i = 0; i < rolls; i++)
        {
            if (_rand.NextDouble() >= selector.Prob)
                continue;

            switch (selector)
            {
                case AllSelector all:
                    foreach (var child in all.Children)
                    {
                        foreach (var spawn in Spawns(child))
                            yield return spawn;
                    }
                    break;
                case NestedSelector nested:
                    foreach (var spawn in Spawns(_data.Tables[nested.TableId.Id]))
                        yield return spawn;
                    break;
                case GroupSelector group:
                    var children = new List<(EntityTableSelector, double)>();
                    foreach (var child in group.Children)
                    {
                        if (CheckConditions(child))
                            children.Add((child, child.Weight));
                    }

                    if (PickWeighted(children) is { } pick)
                    {
                        foreach (var spawn in Spawns(pick))
                            yield return spawn;
                    }
                    break;
                case EntSelector ent:
                    var amount = ent.Amount.Get(_rand);
                    for (var j = 0; j < amount; j++)
                        yield return ent.Id.Id;
                    break;
                case NoneSelector:
                    break;
                default:
                    Warn($"Unknown table selector {selector.GetType().Name}, treated as empty");
                    break;
            }
        }
    }

    private bool CheckConditions(EntityTableSelector selector)
    {
        if (selector.Conditions.Count == 0)
            return true;

        var success = false;
        foreach (var condition in selector.Conditions)
        {
            var res = EvaluateCondition(condition, selector) ^ condition.Invert;
            if (selector.RequireAll && !res)
                return false;

            success |= res;
        }

        return selector.RequireAll || success;
    }

    private bool EvaluateCondition(EntityTableCondition condition, EntityTableSelector root)
    {
        switch (condition)
        {
            case PlayerCountCondition players:
                var count = Connected();
                return count >= players.Min && count <= players.Max;
            case RoundDurationCondition duration:
                var now = TimeSpan.FromSeconds(_now);
                return now >= duration.Min && now <= duration.Max;
            case MaxRuleOccurenceCondition maxOccurrence:
            {
                var rule = maxOccurrence.RuleOverride?.Id ?? (root is EntSelector ent ? ent.Id.Id : string.Empty);
                return rule != string.Empty && Occurrences(rule) < maxOccurrence.Max;
            }
            case ReoccurrenceDelayCondition reoccurrence:
            {
                var rule = reoccurrence.RuleOverride?.Id ?? (root is EntSelector ent ? ent.Id.Id : string.Empty);
                return rule != string.Empty
                    && _history.Any(p => p.Id == rule && p.Time + reoccurrence.Delay.TotalSeconds <= _now);
            }
            case RulesCooldownCondition cooldown:
            {
                // Pending rules use their added time; started rules use their start time.
                var limit = cooldown.Cooldown.TotalSeconds;
                foreach (var instance in _rules)
                {
                    var at = instance.Started ? instance.StartedAt : instance.AddedAt;
                    if (_now - at < limit && cooldown.Rules.Any(r => r.Id == instance.Rule.Id))
                        return false;
                }

                return true;
            }
            case HasBudgetCondition:
                // Scheduler calls supply no budget in the table context.
                return false;
            default:
                var name = condition.GetType().Name;
                if (!_cfg.ConditionPassChance.TryGetValue(name, out var chance))
                {
                    Warn($"No pass chance configured for table condition {name}, assuming it passes");
                    chance = 1;
                }

                return _rand.NextDouble() < chance;
        }
    }

    #endregion

    #region Chaos schedulers (SecretPlusSystem / SecretSaltPlusSystem)

    private void ChaosAdded(ChaosState state)
    {
        var s = state.Scheduler;
        var totalPlayers = TotalPlayers();
        state.ChaosScore = -NextFloat(s.MinStartingChaos * totalPlayers, s.MaxStartingChaos * totalPlayers)
                           * _cfg.RoundstartChaosScoreMultiplier;

        var roll = (float) _rand.NextDouble();
        roll = MathF.Pow(roll, s.ChaosChangeVariationExponent);
        state.ChaosChangeVariation = Lerp(1f, Prob(0.5f) ? s.ChaosChangeVariationMin : s.ChaosChangeVariationMax, roll);

        state.StartingBudget = -state.ChaosScore;
        _summary.StartBudget += state.StartingBudget;
        _summary.ChaosVariation = state.ChaosChangeVariation;

        TrySpawnRoundstartAntags(state);
    }

    private void TrySpawnRoundstartAntags(ChaosState state)
    {
        var s = state.Scheduler;
        if (s.NoRoundstartAntags)
            return;

        if (!_data.WeightTables.TryGetValue(s.PrimaryTable, out var primaryTable)
            || !_data.WeightTables.TryGetValue(s.RoundstartTable, out var roundstartTable))
        {
            Warn($"{s.Id}: missing weight table {s.PrimaryTable} or {s.RoundstartTable}");
            return;
        }

        var count = TotalPlayers();
        var weights = new Dictionary<string, float>(roundstartTable);
        var primaryWeights = new Dictionary<string, float>(primaryTable);
        int maxIters = 50, i = 0;
        var origChaos = state.ChaosScore;

        while (state.ChaosScore < 0 && i < maxIters)
        {
            i++;

            Dictionary<string, float> pickWeights;
            if (i == 1)
            {
                pickWeights = primaryWeights;
                if (s.TpHooks && TpPrimaryCancelled())
                {
                    _summary.TpGreenshiftRoll = true;
                    continue;
                }
            }
            else
            {
                pickWeights = weights;
            }

            if (pickWeights.Count == 0 || pickWeights.Values.All(weight => weight <= 0))
                continue;

            var pick = PickWeighted(pickWeights.Select(p => (p.Key, (double) p.Value)).ToList())!;

            if (!_data.Rules.TryGetValue(pick, out var rule))
                continue;

            if (s.TpHooks && TpRuleFiltered(rule, count))
                continue;

            var chaosScore = ChaosScore(rule, null, null);
            if (chaosScore == null)
                continue;

            var pickProb = (-state.ChaosScore) / chaosScore.Value;
            if (i == 1)
                pickProb *= s.PrimaryAntagChaosBias;
            pickProb = MathF.Min(1f, pickProb);
            if (!Prob(pickProb))
                continue;

            if (!s.IgnoreIncompatible)
            {
                weights.Remove(pick);
                if (_data.IncompatibleModes.TryGetValue(pick, out var incompatible))
                    weights = weights.Where(w => !incompatible.Contains(w.Key)).ToDictionary();
            }

            if (rule.MinPlayers <= count)
            {
                var effPlayers = (int) MathF.Round(count * state.ChaosScore / origChaos);
                StartRule(state, pick, "roundstart", effPlayers);
            }

            if (weights.Count == 0 || (!s.IgnoreIncompatible && rule.LoneRun))
                return;
        }
    }

    private void ChaosTick(ChaosState state, float frameTime)
    {
        var s = state.Scheduler;
        var count = CountActivePlayers();
        var ramp = Ramping(state);
        var speedup = _cfg.EventSpeedup;
        var mult = state.ChaosChangeVariation;

        state.ChaosScore += count.Players * s.LivingChaosChange * frameTime * ramp * speedup * mult;
        state.ChaosScore += count.Ghosts * s.DeadChaosChange * frameTime * speedup * mult;

        if (_now < state.TimeNextEvent)
            return;

        if (!state.FirstEventScheduled)
        {
            state.FirstEventScheduled = true;
            state.TimeNextEvent = _now + _cfg.MinimumTimeUntilFirstEvent / speedup;
            return;
        }

        var amt = NextDouble(s.EventIntervalMin, s.EventIntervalMax) / ramp / speedup;
        state.TimeNextEvent = _now + amt;

        var events = SetupEvents(state, count.Players);
        var selected = ChooseEvent(state, events);
        if (selected != null)
            StartRule(state, selected.Id, "midround", null);
        else
            _summary.EmptyPicks++;
    }

    private float Ramping(ChaosState state)
    {
        return 1f + (float) _now * state.Scheduler.SpeedRamping * _cfg.EventSpeedup;
    }

    private List<SimRule> SetupEvents(ChaosState state, int livingPlayers)
    {
        var s = state.Scheduler;
        var result = new List<SimRule>();

        if (s.Table == null)
        {
            foreach (var rule in _data.StationEvents)
            {
                if (s.DisallowedEvents.Contains(rule.Event!.EventType)
                    || (!s.IgnoreTimings && !CanRun(rule, livingPlayers, _now, 1f / Ramping(state))))
                    continue;

                result.Add(rule);
            }

            return result;
        }

        var limited = LimitedEvents(s.Table,
            s.IgnoreTimings,
            s.IgnoreTimings ? int.MaxValue : null,
            s.IgnoreTimings ? double.MaxValue : null,
            1f / Ramping(state));

        foreach (var rule in limited)
        {
            if (s.DisallowedEvents.Contains(rule.Event!.EventType))
                continue;

            if (s.TpHooks && TpRuleFiltered(rule, livingPlayers))
                continue;

            result.Add(rule);
        }

        return result;
    }

    private SimRule? ChooseEvent(ChaosState state, List<SimRule> possible)
    {
        var s = state.Scheduler;
        var weights = new List<(SimRule, double)>();

        foreach (var ev in possible)
        {
            var chaosScore = ChaosScore(ev, null, null);
            if (chaosScore == null)
                continue;

            var weight = chaosScore.Value;
            var negative = weight < 0f;
            weight = MathF.Abs(weight);
            weight = MathF.Pow(weight, s.ChaosExponent);
            if (negative)
                weight = -weight;
            weight += s.ChaosOffset;
            weight += weight < 0f ? -s.ChaosThreshold : s.ChaosThreshold;
            var delta = ChaosDelta(-state.ChaosScore, weight, s.ChaosMatching, s.ChaosThreshold * s.ChaosThreshold);
            weights.Add((ev, ev.Event!.Weight / (delta + 1f)));
        }

        return weights.Count == 0 ? null : PickWeighted(weights);
    }

    private static float ChaosDelta(float chaos1, float chaos2, float logBase, float differentSignMultiplier)
    {
        var ratio = chaos2 / chaos1;
        if (ratio < 0f)
            ratio = MathF.Abs(chaos2 * chaos1 / differentSignMultiplier);
        return MathF.Abs(MathF.Log(ratio, logBase));
    }

    private void StartRule(ChaosState state, string ruleId, string source, int? players)
    {
        var s = state.Scheduler;
        var rule = _data.Rules[ruleId];
        var playerCount = s.TpHooks ? players ?? TotalPlayers() : 0;

        if (s.TpHooks)
        {
            if (TpRuleFiltered(rule, playerCount))
            {
                _summary.TpFilteredStarts++;
                return;
            }

            if (ChaosScore(rule, null, players) is null)
                return;
        }

        var before = state.ChaosScore;
        var instance = AddRule(ruleId, source, players);
        instance.Scheduler = state;
        var chaosScore = ChaosScore(rule, instance, players);
        if (chaosScore is null)
        {
            if (s.Salt)
                Warn($"{s.Id}: {ruleId} has no chaos score (SecretSaltPlusSystem would throw here)");
            instance.BudgetBefore = -before;
            instance.BudgetAfter = -state.ChaosScore;
            return;
        }

        state.ChaosScore += chaosScore.Value;
        instance.ChaosCost = chaosScore.Value;
        instance.BudgetBefore = -before;
        instance.BudgetAfter = -state.ChaosScore;

        if (players != null && instance.Defs.Count > 0)
        {
            for (var i = 0; i < instance.Defs.Count; i++)
            {
                var target = TargetAntagCount(instance.Defs, players.Value, i);
                instance.Defs[i] = instance.Defs[i] with { Min = target, Max = target };
            }
        }

        if (s.TpHooks && s.Tp != null && rule.Antags != null)
        {
            var cost = 0;
            for (var i = 0; i < rule.Antags.Count; i++)
                cost += TargetAntagCount(instance.Defs, playerCount, i) * DefinitionCost(s.Tp, rule.Antags[i]);

            if (cost > 0)
                _tpReservations[instance] = cost;
        }
    }

    private float? ChaosScore(SimRule rule, RuleInstance? instance, int? players)
    {
        if (rule.Antags != null)
        {
            var defs = instance?.Defs ?? rule.Antags.Select(d => new DefCount(d.Min, d.Max, d.PlayerRatio)).ToList();
            var any = false;
            var score = 0f;
            for (var i = 0; i < rule.Antags.Count; i++)
            {
                if (rule.Antags[i].ChaosScore is not { } defScore)
                    continue;

                any = true;
                score += defScore * TargetAntagCount(defs, players ?? TotalPlayers(), i);
            }

            if (any)
                return score;
        }

        return rule.RuleChaosScore;
    }

    private static int TargetAntagCount(List<DefCount> defs, int poolSize, int index)
    {
        var countOffset = 0;
        foreach (var other in defs)
            countOffset += Math.Clamp((poolSize - countOffset) / SafeRatio(other), other.Min, other.Max) * SafeRatio(other);

        var def = defs[index];
        countOffset -= Math.Clamp(poolSize / SafeRatio(def), def.Min, def.Max) * SafeRatio(def);
        return Math.Clamp((poolSize - countOffset) / SafeRatio(def), def.Min, def.Max);
    }

    private static int SafeRatio(DefCount def) => Math.Max(1, def.PlayerRatio);

    #endregion

    #region SecretTP budget (SecretTPSystem)

    private SimTpBudget? Tp => _tpOwner?.Scheduler.Tp;

    private bool TpPrimaryCancelled()
    {
        var budget = Tp!;
        var total = budget.GreenShiftWeight + budget.RedShiftWeight;
        if (total <= 0)
            return true;

        return NextFloat(0f, total) < budget.GreenShiftWeight;
    }

    private bool TpRuleFiltered(SimRule rule, int playerCount)
    {
        var budget = Tp;
        if (budget == null)
            return false;

        if (budget.RuleBlacklist.Contains(rule.Id))
            return true;

        if (TpMissingDepartments(budget, rule.Id))
            return true;

        return TpEstimateCost(budget, rule, playerCount) > TpAvailable();
    }

    private bool TpMissingDepartments(SimTpBudget budget, string ruleId)
    {
        if (!budget.RuleMinimumAliveDepartments.TryGetValue(ruleId, out var requirements) || requirements.Count == 0)
            return false;

        foreach (var (department, required) in requirements)
        {
            var count = 0;
            foreach (var person in _people)
            {
                if (person.Present && person.Alive && InDepartment(person.Job, department))
                    count++;
            }

            if (count < required)
                return true;
        }

        return false;
    }

    private int TpEstimateCost(SimTpBudget budget, SimRule rule, int players)
    {
        if (rule.Antags == null)
            return 0;

        var defs = rule.Antags.Select(d => new DefCount(d.Min, d.Max, d.PlayerRatio)).ToList();
        var cost = 0;
        for (var i = 0; i < rule.Antags.Count; i++)
        {
            var target = players > 0 ? TargetAntagCount(defs, players, i) : rule.Antags[i].Min;
            cost += target * DefinitionCost(budget, rule.Antags[i]);
        }

        return cost;
    }

    private static int DefinitionCost(SimTpBudget budget, SimAntagDef def)
    {
        if (def.FirstRole != null && budget.AntagPoints.TryGetValue(def.FirstRole, out var roleCost))
            return roleCost;

        foreach (var antag in def.MindRoleAntags)
        {
            if (budget.AntagPoints.TryGetValue(antag, out var mindRoleCost))
                return mindRoleCost;
        }

        return 0;
    }

    private int TpTotal()
    {
        var budget = Tp;
        if (budget == null)
            return 0;

        var total = 0;
        foreach (var person in _people)
        {
            if (person.Present && person.Alive && person.Job != null && budget.JobPoints.TryGetValue(person.Job, out var points))
                total += points;
        }

        return total;
    }

    // Dead antags retain their points during the death grace period.
    private int TpUsed()
    {
        var budget = Tp;
        if (budget == null)
            return 0;

        var used = 0;
        foreach (var person in _people)
        {
            if (person.Antag == null || !person.Present)
                continue;

            if (!person.Alive && _now - person.DeathTime >= budget.DeathReleaseSeconds)
                continue;

            used += person.Antag.Cost;
        }

        return used;
    }

    private int TpAvailable(RuleInstance? exclude = null)
    {
        var reserved = 0;
        foreach (var (rule, points) in _tpReservations)
        {
            if (rule != exclude)
                reserved += points;
        }

        return Math.Max(0, TpTotal() - TpUsed() - reserved);
    }

    #endregion

    #region Antag assignment

    private void AssignAntags(RuleInstance instance)
    {
        var rule = instance.Rule;
        if (rule.Antags == null)
            return;

        var budget = Tp;
        var pool = Connected();
        for (var i = 0; i < rule.Antags.Count; i++)
        {
            var def = rule.Antags[i];
            var count = TargetAntagCount(instance.Defs, pool, i);
            instance.AntagTarget += count;
            var cost = budget != null ? DefinitionCost(budget, def) : 0;

            for (var k = 0; k < count; k++)
            {
                // Charge ghost-role points on takeover; crew picks are charged immediately.
                if (def.PickPlayer && budget != null && cost > 0 && cost > TpAvailable(instance))
                {
                    instance.AntagBlocked += count - k;
                    break;
                }

                SimPerson? person = null;
                if (def.PickPlayer)
                    person = PickCrewAntag();

                if (person == null && def.PickPlayer && !def.HasSpawner)
                {
                    instance.AntagUnfilled += count - k;
                    break;
                }

                if (person != null)
                {
                    if (instance.Roundstart && def.HasSpawner)
                        person.Job = null;

                    GiveAntag(person, instance, def, cost);
                    continue;
                }

                if (!def.HasSpawner)
                {
                    instance.AntagUnfilled++;
                    continue;
                }

                // Untaken ghost-role spawners persist for the whole round.
                _pendingGhostRoles.Add(new PendingGhostRole
                {
                    Instance = instance,
                    Def = def,
                    Cost = cost,
                    Wanted = _rand.NextDouble() < _p.GhostRoleTakeChance,
                });
            }
        }

        SyncReservation(instance);
        RecordWorld(instance);
    }

    private void GiveAntag(SimPerson person, RuleInstance instance, SimAntagDef def, int cost)
    {
        person.Antag = new SimAntag { RuleId = instance.Rule.Id, Label = def.Label, Cost = cost };
        instance.AntagSpawned++;
        instance.AntagLabels[def.Label] = instance.AntagLabels.GetValueOrDefault(def.Label) + 1;
        instance.ArrivalTimes.Add(_now);
        _countsDirty = true;
    }

    // Untaken ghost roles must retain their SecretTP reservation.
    private void SyncReservation(RuleInstance instance)
    {
        var pending = 0;
        foreach (var role in _pendingGhostRoles)
        {
            if (role.Instance == instance)
                pending += role.Cost;
        }

        if (pending > 0)
            _tpReservations[instance] = pending;
        else
            _tpReservations.Remove(instance);
    }

    private void StepGhostRoles(double dt)
    {
        if (_pendingGhostRoles.Count == 0)
            return;

        var rate = Math.Log(2) / Math.Max(0.1, _p.GhostRoleTakeMedianMinutes);
        var chance = 1 - Math.Exp(-rate * dt / 60);
        var budget = Tp;

        for (var i = 0; i < _pendingGhostRoles.Count; i++)
        {
            var role = _pendingGhostRoles[i];
            if (!role.Wanted || _rand.NextDouble() >= chance)
                continue;

            if (budget != null && role.Cost > 0 && role.Cost > TpAvailable(role.Instance))
            {
                role.Instance.AntagBlocked++;
                _pendingGhostRoles.RemoveAt(i--);
                SyncReservation(role.Instance);
                continue;
            }

            if (TakeGhostRole() is not { } person)
                continue;

            GiveAntag(person, role.Instance, role.Def, role.Cost);
            _pendingGhostRoles.RemoveAt(i--);
            SyncReservation(role.Instance);
        }
    }

    private SimPerson? PickCrewAntag()
    {
        var candidates = new List<SimPerson>();
        foreach (var person in _people)
        {
            if (!person.Present || person.Observer || !person.Alive || person.Antag != null || !person.AntagOptIn)
                continue;

            if (_cfg.AntagExcludedDepartments.Any(d => InDepartment(person.Job, d)))
                continue;

            candidates.Add(person);
        }

        return candidates.Count == 0 ? null : candidates[_rand.Next(candidates.Count)];
    }

    private SimPerson? TakeGhostRole()
    {
        var candidates = _people.Where(p => p.Present && (p.Observer || (!p.Alive && p.Ghost))).ToList();
        if (candidates.Count == 0)
            return null;

        var person = candidates[_rand.Next(candidates.Count)];
        person.Observer = false;
        person.Alive = true;
        person.Ghost = false;
        person.ReviveAt = -1;
        person.Job = null;
        return person;
    }

    #endregion

    #region Basic schedulers (BasicStationEventSchedulerSystem)

    private void BasicTick(BasicState state, double dt)
    {
        if (state.TimeUntilNextEvent > 0)
        {
            state.TimeUntilNextEvent -= (float) (dt * _cfg.EventSpeedup);
            return;
        }

        var limited = LimitedEvents(state.Scheduler.Table, false, null, null, 1f);
        var pick = limited.Count == 0 ? null : FindEvent(limited);
        if (pick != null)
            AddRule(pick.Id, state.Scheduler.Id);

        state.TimeUntilNextEvent = _rand.Next(state.Scheduler.MinTiming, state.Scheduler.MaxTiming + 1);
    }

    private SimRule? FindEvent(List<SimRule> events)
    {
        var sumOfWeights = 0f;
        foreach (var ev in events)
            sumOfWeights += ev.Event!.Weight;

        sumOfWeights = NextFloat(0f, sumOfWeights);
        foreach (var ev in events)
        {
            sumOfWeights -= ev.Event!.Weight;
            if (sumOfWeights <= 0f)
                return ev;
        }

        return null;
    }

    #endregion

    #region Output

    private void RecordWorld(RuleInstance instance)
    {
        RefreshCounts();
        instance.Connected = _connected;
        instance.Alive = _alive;
        instance.Ghosts = _ghosts;
        instance.SecAlive = _aliveSec;
        instance.AliveAntags = _aliveAntags;
        instance.Glimmer = _glimmer;
        if (Tp != null)
        {
            instance.TpTotal = TpTotal();
            instance.TpAvailable = TpAvailable();
        }
    }

    private void WriteTimeline()
    {
        RefreshCounts();
        var budget = _chaos.Count > 0 ? -_chaos[0].ChaosScore : 0f;
        _out.Timeline(_presetId, _scenario.Name, _round, _now / 60, _connected, _alive, _ghosts, _aliveAntags,
            _aliveSec, budget, Tp != null ? TpTotal() : null, Tp != null ? TpUsed() : null,
            Tp != null ? TpAvailable() : null, _glimmer);

        if (Tp != null)
            _summary.TpMinAvailable = Math.Min(_summary.TpMinAvailable, TpAvailable());
    }

    private void FinishRound()
    {
        var s = _summary;
        foreach (var role in _pendingGhostRoles)
            role.Instance.AntagUnfilled++;
        _pendingGhostRoles.Clear();

        for (var seq = 0; seq < _rules.Count; seq++)
        {
            var instance = _rules[seq];
            _out.Event(_presetId, _scenario.Name, _round, seq, instance);

            var isAntag = instance.Rule.Antags != null;
            if (instance.Source == "roundstart")
            {
                s.RoundstartRules.Add(instance.Rule.Id);
                s.RoundstartAntags += instance.AntagSpawned;
            }
            else if (instance.Source == "midround")
            {
                s.MidroundEvents++;
                if (isAntag)
                {
                    s.MidroundAntagEvents++;
                    s.MidroundAntags += instance.AntagSpawned;
                    // Ghost-role arrivals can lag behind their event start.
                    foreach (var arrival in instance.ArrivalTimes)
                    {
                        if (s.FirstMidroundAntagMinute == null || arrival / 60 < s.FirstMidroundAntagMinute)
                            s.FirstMidroundAntagMinute = arrival / 60;
                    }
                }
            }
            else
            {
                s.SideEvents++;
            }

            s.AntagBlocked += instance.AntagBlocked;
            s.AntagUnfilled += instance.AntagUnfilled;
        }

        RefreshCounts();
        s.EndConnected = _connected;
        s.EndBudget = _chaos.Count > 0 ? -_chaos[0].ChaosScore : 0f;
        _out.Round(_presetId, _scenario.Name, _round, s);
    }

    #endregion

    #region Random helpers

    private void Warn(string message)
    {
        if (_warned.Add(message))
            _out.Warn(message);
    }

    private bool Prob(float chance) => _rand.NextDouble() < chance;

    private float NextFloat(float min, float max) => min + (float) _rand.NextDouble() * (max - min);

    private double NextDouble(double min, double max) => min + _rand.NextDouble() * (max - min);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private T? PickWeighted<T>(List<(T Item, double Weight)> weights, Random? rng = null) where T : class
    {
        var sum = 0.0;
        foreach (var (_, weight) in weights)
            sum += Math.Max(0, weight);

        if (sum <= 0)
            return null;

        var roll = (rng ?? _rand).NextDouble() * sum;
        foreach (var (item, weight) in weights)
        {
            roll -= Math.Max(0, weight);
            if (roll <= 0)
                return item;
        }

        return weights[^1].Item;
    }

    #endregion
}

public readonly record struct DefCount(int Min, int Max, int PlayerRatio);

public sealed class SimPerson
{
    public int Id;
    public double JoinAt;
    public bool Present;
    public bool InLobby;
    public bool Observer;
    public bool Alive;
    public bool Ghost;
    public bool AntagOptIn;
    public double DeathTime = -1;
    public double ReviveAt = -1;
    public string? Job;
    public SimAntag? Antag;
}

public sealed class SimAntag
{
    public string RuleId = "";
    public string Label = "";
    public int Cost;
}

public sealed class RuleInstance
{
    public SimRule Rule = default!;
    public ChaosState? Scheduler;
    public string Source = "";
    public bool Roundstart;
    public double AddedAt;
    public double StartAt;
    public double StartedAt = -1;
    public bool Started;
    public bool Active;
    public double? EndAt;
    public readonly List<DefCount> Defs = new();

    public float? ChaosCost;
    public float? BudgetBefore;
    public float? BudgetAfter;
    public int AntagTarget;
    public int AntagSpawned;
    public int AntagBlocked;
    public int AntagUnfilled;
    public readonly Dictionary<string, int> AntagLabels = new();

    public readonly List<double> ArrivalTimes = new();

    public int Connected;
    public int Alive;
    public int Ghosts;
    public int SecAlive;
    public int AliveAntags;
    public double Glimmer;
    public int? TpTotal;
    public int? TpAvailable;
}

public sealed class PendingGhostRole
{
    public RuleInstance Instance = default!;
    public SimAntagDef Def = default!;
    public int Cost;

    public bool Wanted;
}

public sealed class ChaosState(SimChaosScheduler scheduler)
{
    public readonly SimChaosScheduler Scheduler = scheduler;
    public float ChaosScore;
    public float ChaosChangeVariation = 1f;
    public float StartingBudget;
    public double TimeNextEvent;
    public bool FirstEventScheduled;
}

public sealed class BasicState(SimBasicScheduler scheduler)
{
    public readonly SimBasicScheduler Scheduler = scheduler;
    public float TimeUntilNextEvent;
}

public sealed class RoundSummary
{
    public int Seed;
    public RoundParams Params = new();
    public int StartConnected;
    public int EndConnected;
    public float StartBudget;
    public float EndBudget;
    public float ChaosVariation = 1f;
    public bool TpGreenshiftRoll;
    public readonly List<string> RoundstartRules = new();
    public int RoundstartAntags;
    public int MidroundEvents;
    public int MidroundAntagEvents;
    public int MidroundAntags;
    public int SideEvents;
    public int EmptyPicks;
    public double? FirstMidroundAntagMinute;
    public int PeakAliveAntags;
    public int Deaths;
    public int SecAtStart;
    public int SecPeak;
    public int AntagBlocked;
    public int AntagUnfilled;
    public int TpStartPoints;
    public int TpMinAvailable = int.MaxValue;
    public int TpFilteredStarts;
}
