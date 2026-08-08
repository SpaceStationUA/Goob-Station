// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions;
using Content.Shared.Localizations;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.EntityEffects;

/// <summary>
/// Pirate compatibility for older downstream entity effects that still use the pre-upstream
/// EntityEffectBaseArgs/EventEntityEffect API.
/// </summary>
public abstract partial class EventEntityEffect<T> : EntityEffectBase<T> where T : EventEntityEffect<T>
{
    public virtual string ReagentEffectFormat => "guidebook-reagent-effect-description";

    protected virtual string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var effect = ReagentEffectGuidebookText(prototype, entSys);
        if (effect is null)
            return null;

        return Loc.GetString(ReagentEffectFormat,
            ("reagent", string.Empty),
            ("quantity", 0),
            ("effect", effect),
            ("chance", Probability),
            ("conditionCount", Conditions?.Length ?? 0),
            ("conditions", ContentLocalizationManager.FormatList(new List<string>())));
    }

    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        base.RaiseEvent(target, raiser, scale, user);

        if (this is not T type)
            return;

        var entMan = IoCManager.Resolve<IEntityManager>();
        var args = LegacyEntityEffectContext.CreateArgs(target, entMan, scale);

        args.User = user;

        var ev = new ExecuteEntityEffectEvent<T>(type, args);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

public static class LegacyEntityEffectContext
{
    private static readonly ConcurrentDictionary<IEntityManager, LegacyEntityEffectReagent> CurrentReagents = new();

    public static IDisposable PushReaction(IEntityManager entityManager, ReactionEntityEvent reaction)
    {
        return PushReagent(
            entityManager,
            null,
            null,
            reaction.ReagentQuantity,
            reaction.Reagent,
            reaction.Method);
    }

    public static IDisposable PushReagent(
        IEntityManager entityManager,
        EntityUid? organEntity,
        Solution? source,
        ReagentQuantity reagentQuantity,
        ReagentPrototype reagent,
        ReactionMethod? method)
    {
        LegacyEntityEffectReagent? previous = null;
        if (CurrentReagents.TryGetValue(entityManager, out var current))
            previous = current;

        CurrentReagents[entityManager] = new LegacyEntityEffectReagent(
            method,
            reagentQuantity,
            reagent,
            organEntity,
            source);

        return new ReagentScope(entityManager, previous);
    }

    public static bool TryGetReagent(IEntityManager entityManager, out LegacyEntityEffectReagent reagent)
    {
        return CurrentReagents.TryGetValue(entityManager, out reagent);
    }

    public static bool TryGetReaction(IEntityManager entityManager, out LegacyEntityEffectReagent reaction)
    {
        if (!TryGetReagent(entityManager, out reaction) || reaction.Method is null)
        {
            reaction = default;
            return false;
        }

        return true;
    }

    public static EntityEffectBaseArgs CreateArgs(EntityUid target, IEntityManager entMan, float scale = 1f)
    {
        if (!TryGetReagent(entMan, out var reagent))
            return new EntityEffectBaseArgs(target, entMan);

        var source = reagent.Source;
        if (source is null)
        {
            source = new Solution();
            source.AddReagent(reagent.ReagentQuantity);
        }

        return new EntityEffectReagentArgs(
            target,
            entMan,
            reagent.OrganEntity,
            source,
            reagent.ReagentQuantity.Quantity,
            reagent.Reagent,
            reagent.Method,
            FixedPoint2.New(scale));
    }

    private sealed class ReagentScope(
        IEntityManager entityManager,
        LegacyEntityEffectReagent? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (previous is { } reaction)
                CurrentReagents[entityManager] = reaction;
            else
                CurrentReagents.TryRemove(entityManager, out _);
        }
    }
}

public readonly record struct LegacyEntityEffectReagent(
    ReactionMethod? Method,
    ReagentQuantity ReagentQuantity,
    ReagentPrototype Reagent,
    EntityUid? OrganEntity,
    Solution? Source);

public static class EntityEffectExt
{
    public static bool ShouldApply(this EntityEffect effect, EntityEffectBaseArgs args, IRobustRandom? random = null)
    {
        if (random == null)
            random = IoCManager.Resolve<IRobustRandom>();

        if (effect.Probability < 1.0f && !random.Prob(effect.Probability))
            return false;

        return true;
    }
}

[ByRefEvent]
public struct ExecuteEntityEffectEvent<T> where T : EntityEffectBase<T>
{
    public T Effect;
    public EntityEffectBaseArgs Args;

    public ExecuteEntityEffectEvent(T effect, EntityEffectBaseArgs args)
    {
        Effect = effect;
        Args = args;
    }
}

public record class EntityEffectBaseArgs
{
    public EntityUid TargetEntity;

    public IEntityManager EntityManager = default!;

    public FixedPoint2 Scale = FixedPoint2.New(1);

    public EntityUid? User;

    public EntityEffectBaseArgs(EntityUid targetEntity, IEntityManager entityManager)
    {
        TargetEntity = targetEntity;
        EntityManager = entityManager;
    }
}

public record class EntityEffectReagentArgs : EntityEffectBaseArgs
{
    public EntityUid? OrganEntity;

    public Solution? Source;

    public FixedPoint2 Quantity;

    public ReagentPrototype? Reagent;

    public ReactionMethod? Method;

    public EntityEffectReagentArgs(EntityUid targetEntity,
        IEntityManager entityManager,
        EntityUid? organEntity,
        Solution? source,
        FixedPoint2 quantity,
        ReagentPrototype? reagent,
        ReactionMethod? method,
        FixedPoint2 scale)
        : base(targetEntity, entityManager)
    {
        OrganEntity = organEntity;
        Source = source;
        Quantity = quantity;
        Reagent = reagent;
        Method = method;
        Scale = scale;
    }
}

public abstract partial class EntityEffectCondition : EntityCondition
{
    public abstract bool Condition(EntityEffectBaseArgs args);

    public abstract string GuidebookExplanation(IPrototypeManager prototype);

    public override bool RaiseEvent(EntityUid target, IEntityConditionRaiser raiser)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        return Condition(LegacyEntityEffectContext.CreateArgs(target, entMan));
    }

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return GuidebookExplanation(prototype);
    }
}

[ByRefEvent]
public struct CheckEntityEffectConditionEvent<T> where T : EntityEffectCondition
{
    public T Condition;
    public EntityEffectBaseArgs Args;
    public bool Result;
}

public abstract partial class EventEntityEffectCondition<T> : EntityEffectCondition where T : EventEntityEffectCondition<T>
{
    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (this is not T type)
            return false;

        var evt = new CheckEntityEffectConditionEvent<T> { Condition = type, Args = args };
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref evt);
        return evt.Result;
    }
}
