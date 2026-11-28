// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Shared._Pirate.Arenas;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._Shitmed.Restrict;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Pirate.Arenas;

/// <summary>Owns the lifetime of an arena match and its optional game-time clock.</summary>
public abstract partial class ArenaRuleSystem<T> : EntitySystem where T : Component
{
    [Dependency] protected readonly GameTicker GameTicker = default!;
    [Dependency] protected readonly IChatManager ChatManager = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IRobustRandom RobustRandom = default!;

    protected Entity<T>? ActiveRule { get; private set; }
    private CancellationTokenSource? _clock;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<T, GameRuleStartedEvent>(OnStarted);
        SubscribeLocalEvent<T, GameRuleEndedEvent>(OnEnded);
        SubscribeLocalEvent<T, ComponentShutdown>(OnRuleShutdown);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnStarted(Entity<T> ent, ref GameRuleStartedEvent args)
    {
        if (!TryComp<GameRuleComponent>(ent, out var gameRule))
            return;
        if (ActiveRule is {} current && current.Owner != ent.Owner && GameTicker.IsGameRuleActive(current))
        {
            GameTicker.EndGameRule(ent);
            return;
        }
        StopClock();
        ActiveRule = ent;
        Started(ent, ent.Comp, gameRule, args);
    }

    private void OnEnded(Entity<T> ent, ref GameRuleEndedEvent args)
    {
        if (ActiveRule?.Owner != ent.Owner || !TryComp<GameRuleComponent>(ent, out var gameRule))
            return;
        StopClock();
        Ended(ent, ent.Comp, gameRule, args);
        ActiveRule = null;
    }

    private void OnRuleShutdown(EntityUid uid, T component, ComponentShutdown args)
    {
        if (ActiveRule?.Owner != uid)
            return;
        StopClock();
        ActiveRule = null;
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (ActiveRule is {} ent && TryComp<GameRuleComponent>(ent, out var rule))
            AppendRoundEndText(ent, ent.Comp, rule, ref args);
    }

    protected void AllowArenaGunUse(EntityUid weapon)
    {
        if (HasComp<ChamberMagazineAmmoProviderComponent>(weapon))
            EnsureComp<ArenaMagazineDisplayComponent>(weapon);

        if (!TryComp<RestrictGunshotsByUserTagComponent>(weapon, out var restriction) ||
            !restriction.DoesntContain.Contains("Oni"))
            return;

        // Only the issued weapon changes. Keep every other firing restriction and the original prototype.
        restriction.DoesntContain = [.. restriction.DoesntContain];
        restriction.DoesntContain.Remove("Oni");
        Dirty(weapon, restriction);
    }

    protected CancellationToken MatchToken => (_clock ??= new CancellationTokenSource()).Token;

    protected void ScheduleOnce(TimeSpan delay, Action callback)
    {
        Timer.Spawn(delay, callback, MatchToken);
    }

    protected void StartClock(TimeSpan interval, Action tick)
    {
        StopClock();
        _clock = new CancellationTokenSource();
        Timer.SpawnRepeating(interval, tick, _clock.Token);
    }

    protected void StopClock()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = null;
    }

    protected virtual void Started(EntityUid uid, T rule, GameRuleComponent gameRule, GameRuleStartedEvent args) { }
    protected virtual void Ended(EntityUid uid, T rule, GameRuleComponent gameRule, GameRuleEndedEvent args) { }
    protected virtual void AppendRoundEndText(EntityUid uid, T rule, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args) { }
}
