#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Content.IntegrationTests.Tests._Pirate.GameModeSim;

public sealed class GameModeSimOutput : IDisposable
{
    private readonly StreamWriter _rounds;
    private readonly StreamWriter _events;
    private readonly StreamWriter _timeline;
    public readonly List<string> Warnings = new();

    public GameModeSimOutput(string dir)
    {
        Directory.CreateDirectory(dir);
        _rounds = new StreamWriter(Path.Combine(dir, "rounds.csv"));
        _events = new StreamWriter(Path.Combine(dir, "events.csv"));
        _timeline = new StreamWriter(Path.Combine(dir, "timeline.csv"));

        _rounds.WriteLine("preset,scenario,round,seed,startConnected,endConnected,startBudget,endBudget,chaosVariation," +
                          "tpGreenshiftRoll,primaryRule,roundstartRules,roundstartAntags,midroundEvents,midroundAntagEvents," +
                          "midroundAntags,totalAntags,sideEvents,emptyPicks,firstMidroundAntagMin,peakAliveAntags,deaths," +
                          "secAtStart,secPeak,antagBlocked,antagUnfilled,tpStartPoints,tpMinAvailable,tpFilteredStarts," +
                          string.Join(',', RoundParams.All.Select(p => "p_" + p.Name)));
        _events.WriteLine("preset,scenario,round,seq,source,rule,eventType,isAntag,addedMin,startedMin,chaosCost,budgetBefore," +
                          "budgetAfter,antagTarget,antagSpawned,antagBlocked,antagUnfilled,antags,connected,alive,ghosts," +
                          "secAlive,aliveAntags,glimmer,tpTotal,tpAvailable,arrivals");
        _timeline.WriteLine("preset,scenario,round,minute,connected,alive,ghosts,aliveAntags,secAlive,budget,tpTotal," +
                            "tpUsed,tpAvailable,glimmer");
    }

    public void Round(string preset, string scenario, int round, RoundSummary s)
    {
        Row(_rounds, preset, scenario, round, s.Seed, s.StartConnected, s.EndConnected, s.StartBudget, s.EndBudget,
            s.ChaosVariation, s.TpGreenshiftRoll ? 1 : 0, s.RoundstartRules.FirstOrDefault() ?? "",
            string.Join('+', s.RoundstartRules), s.RoundstartAntags, s.MidroundEvents, s.MidroundAntagEvents,
            s.MidroundAntags, s.RoundstartAntags + s.MidroundAntags, s.SideEvents, s.EmptyPicks,
            s.FirstMidroundAntagMinute, s.PeakAliveAntags, s.Deaths, s.SecAtStart, s.SecPeak, s.AntagBlocked,
            s.AntagUnfilled, s.TpStartPoints, s.TpMinAvailable == int.MaxValue ? null : s.TpMinAvailable,
            s.TpFilteredStarts, RoundParams.All.Select(p => (object?) p.Get(s.Params)).ToArray());
    }

    public void Event(string preset, string scenario, int round, int seq, RuleInstance i)
    {
        var antags = string.Join(';', i.AntagLabels.Select(p => $"{p.Key}:{p.Value}"));
        Row(_events, preset, scenario, round, seq, i.Source, i.Rule.Id, i.Rule.Event?.EventType ?? "",
            i.Rule.Antags != null ? 1 : 0, i.AddedAt / 60, i.StartedAt >= 0 ? i.StartedAt / 60 : null, i.ChaosCost,
            i.BudgetBefore, i.BudgetAfter, i.AntagTarget, i.AntagSpawned, i.AntagBlocked, i.AntagUnfilled, antags,
            i.Connected, i.Alive, i.Ghosts, i.SecAlive, i.AliveAntags, i.Glimmer, i.TpTotal, i.TpAvailable,
            string.Join(';', i.ArrivalTimes.Select(t => (t / 60).ToString("0.##", CultureInfo.InvariantCulture))));
    }

    public void Timeline(string preset, string scenario, int round, double minute, int connected, int alive, int ghosts,
        int aliveAntags, int secAlive, float budget, int? tpTotal, int? tpUsed, int? tpAvailable, double glimmer)
    {
        Row(_timeline, preset, scenario, round, minute, connected, alive, ghosts, aliveAntags, secAlive, budget,
            tpTotal, tpUsed, tpAvailable, glimmer);
    }

    public void Warn(string message) => Warnings.Add(message);

    private static void Row(StreamWriter writer, params object?[] values)
    {
        var first = true;
        foreach (var value in values)
        {
            foreach (var cell in value is object?[] cells ? cells : [value])
            {
                if (!first)
                    writer.Write(',');
                writer.Write(Format(cell));
                first = false;
            }
        }

        writer.WriteLine();
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "",
            float f => f.ToString("0.##", CultureInfo.InvariantCulture),
            double d => d.ToString("0.#####", CultureInfo.InvariantCulture),
            string s when s.Contains(',') || s.Contains('"') => $"\"{s.Replace("\"", "\"\"")}\"",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    public void Dispose()
    {
        _rounds.Dispose();
        _events.Dispose();
        _timeline.Dispose();
    }
}
