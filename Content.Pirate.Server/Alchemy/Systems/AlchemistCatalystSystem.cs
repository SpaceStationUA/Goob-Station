using Content.Pirate.Shared.Alchemy.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistCatalystSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private const string CatalystEffect = "StatusEffectAlchemistCatalyst";
    private readonly HashSet<EntityUid> _boosting = [];
    private readonly Dictionary<EntityUid, EntProtoId> _pendingEffects = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistCatalystComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffectAdded);
        SubscribeLocalEvent<AlchemistCatalystComponent, ComponentShutdown>(OnCatalystShutdown);
        SubscribeLocalEvent<StatusEffectComponent, StatusEffectEndTimeUpdatedEvent>(OnEndTimeUpdated);
    }

    private void OnBeforeStatusEffectAdded(Entity<AlchemistCatalystComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (args.Effect == CatalystEffect || _pendingEffects.ContainsKey(ent.Owner))
            return;

        _pendingEffects[ent.Owner] = args.Effect;
    }

    private void OnCatalystShutdown(Entity<AlchemistCatalystComponent> ent, ref ComponentShutdown args)
    {
        _pendingEffects.Remove(ent.Owner);
    }

    private void OnEndTimeUpdated(Entity<StatusEffectComponent> ent, ref StatusEffectEndTimeUpdatedEvent args)
    {
        if (!HasComp<AlchemistCatalystComponent>(args.Target)
            || args.EndTime is null
            || !_boosting.Add(ent.Owner))
            return;

        try
        {
            if (MetaData(ent).EntityPrototype is not { } effectProto || effectProto.ID == CatalystEffect)
                return;

            if (_pendingEffects.TryGetValue(args.Target, out var pending) && pending != effectProto.ID)
                return;

            var remaining = args.EndTime.Value - _timing.CurTime;
            if (remaining <= TimeSpan.Zero)
                return;

            _statusEffects.TryAddTime(args.Target, effectProto.ID, remaining);
            _pendingEffects.Remove(args.Target);
            _statusEffects.TryRemoveStatusEffect(args.Target, CatalystEffect);
        }
        finally
        {
            _boosting.Remove(ent.Owner);
        }
    }
}
